using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AportesController : ControllerBase
{
    /// <summary>Nombre de la categoría de ahorro que usan los movimientos generados por aportes.</summary>
    private const string NombreCategoriaAhorro = "Otros ahorros";

    private readonly ApplicationDbContext _db;

    public AportesController(ApplicationDbContext db) => _db = db;

    [ProducesResponseType(typeof(IEnumerable<AporteResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AporteResponseDto>>> GetAportes([FromQuery] int metaId)
    {
        var idHogar = await ObtenerHogarActualAsync();
        if (idHogar is null)
            return Forbid();

        var metaPertenece = await _db.MetasAhorro.AsNoTracking()
            .AnyAsync(m => m.IdMeta == metaId && m.IdHogar == idHogar.Value);
        if (!metaPertenece)
            return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });

        var aportes = await _db.Aportes.AsNoTracking()
            .Where(a => a.IdMeta == metaId && a.IdHogar == idHogar.Value)
            .OrderByDescending(a => a.Fecha)
            .Select(a => new AporteResponseDto(a.IdAporte, a.IdMeta, a.Monto, a.Fecha, a.Notas))
            .ToListAsync();

        return Ok(aportes);
    }

    [ProducesResponseType(typeof(AporteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<ActionResult<AporteResponseDto>> Create([FromBody] CrearAporteRequest request)
    {
        if (request.Monto <= 0 || request.IdempotencyKey == Guid.Empty)
            return BadRequest(new { message = "El monto debe ser positivo y se requiere una clave de idempotencia." });

        var idUsuario = User.GetIdUsuario();
        var idHogar = await ObtenerHogarActualAsync();
        if (idHogar is null)
            return Forbid();

        var repetido = await _db.Aportes.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdempotencyKey == request.IdempotencyKey);
        if (repetido is not null)
            return ResultadoIdempotente(repetido, request, idHogar.Value, idUsuario);

        var meta = await _db.MetasAhorro.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdMeta == request.IdMeta && m.IdHogar == idHogar.Value);
        if (meta is null)
            return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });

        var aporte = new AporteMeta
        {
            IdMeta = request.IdMeta,
            IdHogar = idHogar.Value,
            IdUsuario = idUsuario,
            IdempotencyKey = request.IdempotencyKey,
            Monto = request.Monto,
            Fecha = request.Fecha ?? DateTime.UtcNow,
            Notas = request.Notas?.Trim(),
        };

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

        try
        {
            if (_db.Database.IsRelational())
            {
                // Update atómico evita perder aportes concurrentes y mantiene el estado de la meta.
                var actualizadas = await _db.MetasAhorro
                    .Where(m => m.IdMeta == request.IdMeta && m.IdHogar == idHogar.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.MontoActual, m => m.MontoActual + request.Monto)
                        .SetProperty(m => m.Estado, m => m.MontoActual + request.Monto >= m.MontoObjetivo
                            ? "Completada"
                            : m.Estado));

                if (actualizadas != 1)
                {
                    if (transaction is not null) await transaction.RollbackAsync();
                    return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });
                }
            }
            else
            {
                // EF InMemory no implementa ExecuteUpdate; se conserva una ruta para unit tests.
                var metaActualizada = await _db.MetasAhorro
                    .FirstOrDefaultAsync(m => m.IdMeta == request.IdMeta && m.IdHogar == idHogar.Value);
                if (metaActualizada is null)
                    return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });

                metaActualizada.MontoActual += request.Monto;
                if (metaActualizada.MontoActual >= metaActualizada.MontoObjetivo)
                    metaActualizada.Estado = "Completada";
            }

            _db.Aportes.Add(aporte);
            await _db.SaveChangesAsync();

            // El aporte deja rastro en la lista de movimientos (tipo "Ahorro"), con el
            // mismo IdUsuario que aportó. Se hace dentro de la MISMA transacción: si el
            // aporte se revierte (p. ej. colisión de la clave de idempotencia), el
            // movimiento también, así que el reintento nunca lo duplica.
            var categoriaAhorro = await ObtenerOCrearCategoriaAhorroAsync(idHogar.Value);
            _db.Movimientos.Add(CrearMovimientoAhorro(
                aporte, idHogar.Value, idUsuario, categoriaAhorro.IdCategoria, meta.Titulo));
            await _db.SaveChangesAsync();

            if (transaction is not null) await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetAportes), new { metaId = aporte.IdMeta }, ToResponse(aporte));
        }
        catch (DbUpdateException ex) when (EsColisionIdempotencia(ex))
        {
            if (transaction is not null) await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            var duplicado = await _db.Aportes.AsNoTracking()
                .FirstOrDefaultAsync(a => a.IdempotencyKey == request.IdempotencyKey);
            if (duplicado is not null)
                return ResultadoIdempotente(duplicado, request, idHogar.Value, idUsuario);
            return Conflict(new { message = "No se pudo confirmar el aporte. Inténtalo nuevamente." });
        }
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var idHogar = await ObtenerHogarActualAsync();
        if (idHogar is null)
            return Forbid();

        var aporte = await _db.Aportes
            .FirstOrDefaultAsync(a => a.IdAporte == id && a.IdHogar == idHogar.Value);
        if (aporte is null)
            return NotFound();

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

        if (_db.Database.IsRelational())
        {
            var actualizadas = await _db.MetasAhorro
                .Where(m => m.IdMeta == aporte.IdMeta && m.IdHogar == idHogar.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.MontoActual, m => m.MontoActual >= aporte.Monto
                        ? m.MontoActual - aporte.Monto
                        : 0m)
                    .SetProperty(m => m.Estado, m => m.MontoActual - aporte.Monto < m.MontoObjetivo
                        ? "En progreso"
                        : m.Estado));

            if (actualizadas != 1)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                return NotFound();
            }
        }
        else
        {
            var meta = await _db.MetasAhorro
                .FirstOrDefaultAsync(m => m.IdMeta == aporte.IdMeta && m.IdHogar == idHogar.Value);
            if (meta is null)
                return NotFound();
            meta.MontoActual = Math.Max(0, meta.MontoActual - aporte.Monto);
            if (meta.MontoActual < meta.MontoObjetivo)
                meta.Estado = "En progreso";
        }

        // El movimiento de ahorro generado por el aporte se borra en la misma
        // transacción. La marca "aporte:{id}" garantiza que solo se elimine el
        // movimiento de ESTE aporte y no otro movimiento del hogar. Los aportes
        // anteriores a esta feature no tienen movimiento, así que no hay nada que borrar.
        var movimiento = await _db.Movimientos
            .FirstOrDefaultAsync(m => m.IdHogar == idHogar.Value
                                   && m.Tipo == "Ahorro"
                                   && m.OrigenEmisora == MarcaAporte(aporte));
        if (movimiento is not null)
            _db.Movimientos.Remove(movimiento);

        _db.Aportes.Remove(aporte);
        await _db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return NoContent();
    }

    /// <summary>
    /// Devuelve la categoría de ahorro del hogar que usan los movimientos de aportes.
    /// Es determinista y estable por hogar: siempre "Otros ahorros" (la categoría
    /// genérica ya sembrada por CategoriasSistema y por la migración Fase5), y se
    /// reutiliza entre aportes; no se crea una categoría por aporte. Si el hogar no
    /// la tiene, la crea de forma idempotente.
    /// </summary>
    private async Task<Categoria> ObtenerOCrearCategoriaAhorroAsync(int idHogar)
    {
        var normalizado = CategoriasSistema.Normalizar(NombreCategoriaAhorro);

        // El plegado de nombres se aplica en memoria: por hogar hay muy pocas
        // categorías de ahorro y así no se depende de la traducción del proveedor.
        var existentes = await _db.Categorias.AsNoTracking()
            .Where(c => c.IdHogar == idHogar && c.Tipo == "Ahorro")
            .ToListAsync();

        var categoria = existentes
            .FirstOrDefault(c => CategoriasSistema.Normalizar(c.Nombre) == normalizado);
        if (categoria is not null)
            return categoria;

        // Mismo nombre e icono que los que siembran CategoriasSistema y la migración Fase5.
        categoria = new Categoria
        {
            IdHogar = idHogar,
            Nombre = NombreCategoriaAhorro,
            Tipo = "Ahorro",
            Icono = "\uD83D\uDC37",
            EsSistema = true
        };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        return categoria;
    }

    /// <summary>
    /// Movimiento de tipo "Ahorro" que representa el aporte en la lista de movimientos.
    /// OrigenEmisora guarda la marca que enlaza el movimiento con su aporte.
    /// </summary>
    private static Movimiento CrearMovimientoAhorro(
        AporteMeta aporte, int idHogar, int idUsuario, int idCategoria, string? tituloMeta)
        => new()
        {
            IdHogar = idHogar,
            IdUsuario = idUsuario,
            IdCategoria = idCategoria,
            Monto = aporte.Monto,
            Fecha = aporte.Fecha,
            Tipo = "Ahorro",
            Descripcion = string.IsNullOrWhiteSpace(tituloMeta)
                ? "Aporte a meta de ahorro"
                : $"Aporte a la meta {tituloMeta}",
            OrigenEmisora = MarcaAporte(aporte)
        };

    /// <summary>Marca única e inmutable que enlaza un movimiento de ahorro con su aporte.</summary>
    private static string MarcaAporte(AporteMeta aporte)
        => $"aporte:{aporte.IdAporte}";

    private async Task<int?> ObtenerHogarActualAsync()
    {
        // Usa la membresía vigente en la base, no un claim IdHogar que podría estar obsoleto.
        return await _db.Usuarios.AsNoTracking()
            .Where(u => u.IdUsuario == User.GetIdUsuario())
            .Select(u => u.IdHogar)
            .FirstOrDefaultAsync();
    }

    private static AporteResponseDto ToResponse(AporteMeta aporte)
        => new(aporte.IdAporte, aporte.IdMeta, aporte.Monto, aporte.Fecha, aporte.Notas);

    private ActionResult<AporteResponseDto> ResultadoIdempotente(
        AporteMeta existente, CrearAporteRequest request, int idHogar, int idUsuario)
    {
        var mismaFecha = request.Fecha is null || existente.Fecha == request.Fecha.Value;
        if (existente.IdHogar != idHogar
            || existente.IdUsuario != idUsuario
            || existente.IdMeta != request.IdMeta
            || existente.Monto != request.Monto
            || existente.Notas != request.Notas?.Trim()
            || !mismaFecha)
            return Conflict(new { message = "La clave de idempotencia ya fue usada para otro aporte." });

        return Ok(ToResponse(existente));
    }

    private static bool EsColisionIdempotencia(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.UniqueViolation
           && postgres.ConstraintName == "IX_Aportes_IdempotencyKey";
}

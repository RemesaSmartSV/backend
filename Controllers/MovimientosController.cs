using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MovimientosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;

    public MovimientosController(ApplicationDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    /// <summary>Obtiene los movimientos del hogar con filtros opcionales y paginación.</summary>
    /// <param name="categoriaId">Filtra por identificador de categoría cuando se especifica.</param>
    /// <param name="tipo">Filtra por tipo de movimiento cuando se especifica.</param>
    /// <param name="fechaInicio">Filtra los movimientos desde esta fecha inclusive.</param>
    /// <param name="fechaFin">Filtra los movimientos hasta esta fecha inclusive.</param>
    /// <param name="usuarioId">Filtra por identificador del miembro que registró el movimiento cuando se especifica.</param>
    /// <param name="page">Número de página que se consultará.</param>
    /// <param name="pageSize">Cantidad de movimientos por página, entre 1 y 100.</param>
    /// <response code="200">Devuelve la página de movimientos que coincide con los filtros.</response>
    /// <response code="400">Los valores de paginación están fuera de los límites permitidos.</response>
    [ProducesResponseType(typeof(PaginatedResponse<Movimiento>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Movimiento>>> GetMovimientos(
        [FromQuery] int? categoriaId,
        [FromQuery] string? tipo,
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        [FromQuery] int? usuarioId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page debe ser mayor o igual a 1 y pageSize debe estar entre 1 y 100." });

        var idHogar = User.GetIdHogar();
        var query = _db.Movimientos.AsNoTracking().Where(m => m.IdHogar == idHogar);

        if (categoriaId.HasValue)
            query = query.Where(m => m.IdCategoria == categoriaId.Value);
        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(m => m.Tipo == tipo);
        if (fechaInicio.HasValue)
            query = query.Where(m => m.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)
            query = query.Where(m => m.Fecha <= fechaFin.Value);
        if (usuarioId.HasValue)
            query = query.Where(m => m.IdUsuario == usuarioId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<Movimiento>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>Obtiene un movimiento del hogar por su identificador.</summary>
    /// <param name="id">Identificador del movimiento.</param>
    /// <response code="200">Devuelve el movimiento encontrado.</response>
    /// <response code="404">El movimiento no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(Movimiento), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<ActionResult<Movimiento>> GetMovimiento(int id)
    {
        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        return movimiento is null ? NotFound() : Ok(movimiento);
    }

    /// <summary>Exporta los movimientos del hogar como archivo CSV o JSON.</summary>
    /// <param name="formato">Formato de exportación: csv o json.</param>
    /// <response code="200">Devuelve el archivo de movimientos en el formato solicitado.</response>
    /// <response code="400">El formato no es csv ni json.</response>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar([FromQuery] string formato = "csv")
    {
        if (!string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(formato, "json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El formato debe ser csv o json." });

        var movimientos = await _db.Movimientos
            .AsNoTracking()
            .Where(m => m.IdHogar == User.GetIdHogar())
            .OrderByDescending(m => m.Fecha)
            .Select(m => new
            {
                id = m.IdMovimiento,
                fecha = m.Fecha,
                tipo = m.Tipo,
                categoria = m.Categoria.Nombre,
                monto = m.Monto,
                descripcion = m.Descripcion,
                origen = m.OrigenEmisora
            })
            .ToListAsync();

        var nombreArchivo = $"movimientos-{DateTime.UtcNow:yyyyMMdd}.";

        if (string.Equals(formato, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(movimientos, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            return File(Encoding.UTF8.GetBytes(json), "application/json", nombreArchivo + "json");
        }

        var csv = new StringBuilder();
        csv.AppendLine("Id,Fecha,Tipo,Categoria,Monto,Descripcion,Origen");

        foreach (var movimiento in movimientos)
        {
            csv.AppendLine(string.Join(",",
                movimiento.id,
                EscaparCsv(movimiento.fecha.ToString("O", CultureInfo.InvariantCulture)),
                EscaparCsv(movimiento.tipo),
                EscaparCsv(movimiento.categoria),
                movimiento.monto.ToString("0.00", CultureInfo.InvariantCulture),
                EscaparCsv(movimiento.descripcion),
                EscaparCsv(movimiento.origen)));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", nombreArchivo + "csv");
    }

    private static string EscaparCsv(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        return $"\"{valor.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Crea un movimiento para el hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "monto": 18.75, "fecha": "2026-09-28T12:00:00Z", "tipo": "Gasto", "descripcion": "Compra de alimentos", "origenEmisora": "Transferencia"}</c></remarks>
    /// <param name="movimiento">Datos del movimiento que se creará.</param>
    /// <response code="201">El movimiento fue creado.</response>
    /// <response code="400">El tipo no es válido o la categoría no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(Movimiento), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<Movimiento>> Create([FromBody] Movimiento movimiento)
    {
        if (!EsTipoMovimientoValido(movimiento.Tipo))
            return BadRequest(new { message = "El tipo debe ser Ingreso, Gasto o Ahorro." });

        var idHogar = User.GetIdHogar();
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == movimiento.IdCategoria && c.IdHogar == idHogar);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });

        movimiento.IdMovimiento = 0;
        movimiento.IdHogar = idHogar;
        movimiento.IdUsuario = User.GetIdUsuario();
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();

        // Alerta de presupuesto: si es un gasto y cruza el 80% o 100% del límite,
        // envía correo (una vez por cruce, no por cada movimiento).
        if (movimiento.Tipo == "Gasto" && movimiento.IdCategoria > 0)
        {
            _ = VerificarAlertaPresupuesto(movimiento);
        }

        return CreatedAtAction(nameof(GetMovimiento), new { id = movimiento.IdMovimiento }, movimiento);
    }

    private async Task VerificarAlertaPresupuesto(Movimiento movimiento)
    {
        try
        {
            var mesAnio = new DateTime(movimiento.Fecha.Year, movimiento.Fecha.Month, 1);
            var presupuesto = await _db.Presupuestos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdHogar == movimiento.IdHogar
                    && p.IdCategoria == movimiento.IdCategoria
                    && p.MesAnio == mesAnio);

            if (presupuesto is null) return;

            var gastado = await _db.Movimientos
                .AsNoTracking()
                .Where(m => m.IdHogar == movimiento.IdHogar
                    && m.IdCategoria == movimiento.IdCategoria
                    && m.Tipo == "Gasto"
                    && m.Fecha.Year == movimiento.Fecha.Year
                    && m.Fecha.Month == movimiento.Fecha.Month)
                .SumAsync(m => (decimal?)m.Monto) ?? 0m;

            var porcentaje = presupuesto.MontoLimite > 0
                ? Math.Round(gastado / presupuesto.MontoLimite * 100, 1)
                : 0;

            var usuario = await _db.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdUsuario == movimiento.IdUsuario);

            if (usuario is null) return;

            if (gastado > presupuesto.MontoLimite)
            {
                await _email.EnviarPresupuestoExcedidoAsync(
                    usuario.Correo, usuario.Nombre,
                    presupuesto.Categoria?.Nombre ?? "Presupuesto",
                    presupuesto.MontoLimite, gastado);
            }
            else if (porcentaje >= 80)
            {
                await _email.EnviarAlertaPresupuestoAsync(
                    usuario.Correo, usuario.Nombre,
                    presupuesto.Categoria?.Nombre ?? "Presupuesto",
                    presupuesto.MontoLimite, gastado, porcentaje);
            }
        }
        catch
        {
            // La alerta no debe bloquear la operación financiera.
        }
    }

    /// <summary>Actualiza un movimiento del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "monto": 18.75, "fecha": "2026-09-28T12:00:00Z", "tipo": "Gasto", "descripcion": "Compra de alimentos", "origenEmisora": "Transferencia"}</c></remarks>
    /// <param name="id">Identificador del movimiento.</param>
    /// <param name="input">Nuevos datos del movimiento.</param>
    /// <response code="204">El movimiento fue actualizado.</response>
    /// <response code="400">El tipo no es válido o la categoría no existe o no pertenece al hogar.</response>
    /// <response code="404">El movimiento no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Movimiento input)
    {
        if (!EsTipoMovimientoValido(input.Tipo))
            return BadRequest(new { message = "El tipo debe ser Ingreso, Gasto o Ahorro." });

        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        if (movimiento is null)
            return NotFound();

        if (input.IdCategoria != movimiento.IdCategoria)
        {
            var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == input.IdCategoria && c.IdHogar == User.GetIdHogar());
            if (categoria is null)
                return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });
            movimiento.IdCategoria = input.IdCategoria;
        }

        movimiento.Monto = input.Monto;
        movimiento.Fecha = input.Fecha;
        movimiento.Tipo = input.Tipo;
        movimiento.Descripcion = input.Descripcion;
        movimiento.OrigenEmisora = input.OrigenEmisora;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool EsTipoMovimientoValido(string? tipo)
        => string.Equals(tipo, "Ingreso", StringComparison.Ordinal) ||
           string.Equals(tipo, "Gasto", StringComparison.Ordinal) ||
           string.Equals(tipo, "Ahorro", StringComparison.Ordinal);

    /// <summary>Elimina un movimiento del hogar.</summary>
    /// <param name="id">Identificador del movimiento.</param>
    /// <response code="204">El movimiento fue eliminado.</response>
    /// <response code="404">El movimiento no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        if (movimiento is null)
            return NotFound();
        _db.Movimientos.Remove(movimiento);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Obtiene el resumen de ingresos, gastos, ahorro y balance del hogar.</summary>
    /// <param name="anio">Año del resumen. Con <c>mes</c> filtra ese mes; solo, filtra el año completo en UTC.</param>
    /// <param name="mes">Mes del resumen; requiere <c>anio</c> para aplicarse.</param>
    /// <param name="fechaInicio">Inicio del rango (inclusive). Tiene prioridad sobre anio/mes.</param>
    /// <param name="fechaFin">Fin del rango (inclusive). Tiene prioridad sobre anio/mes.</param>
    /// <remarks>
    /// Prelación de filtros: si vienen <c>fechaInicio</c> y/o <c>fechaFin</c>, se usa ese rango
    /// (abierto si falta uno de los dos); si no, anio+mes filtra el mes; si no, solo anio
    /// filtra el año completo en UTC; si no, se devuelve el historial completo.
    /// El ahorro suma los aportes (AporteMeta) del período más los movimientos de tipo
    /// "Ahorro" no generados por un aporte. Balance = Ingresos - Gastos - Ahorro.
    /// </remarks>
    /// <response code="200">Devuelve los totales del período consultado.</response>
    [ProducesResponseType(typeof(ResumenDashboardDTO), StatusCodes.Status200OK)]
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenDashboardDTO>> GetResumen(
        [FromQuery] int? anio,
        [FromQuery] int? mes,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null)
    {
        var idHogar = User.GetIdHogar();

        // Prelación: rango explícito (fechaInicio/fechaFin) > anio+mes > solo anio > historial completo.
        var usarRangoFechas = fechaInicio.HasValue || fechaFin.HasValue;
        DateTime? inicioRango = fechaInicio;
        DateTime? finRango = fechaFin;

        if (!usarRangoFechas && anio.HasValue && !mes.HasValue)
        {
            // Corrección: antes un anio sin mes se ignoraba en silencio y se devolvía todo.
            // Ahora el año solo filtra el año completo (1 de enero al 31 de diciembre, UTC).
            inicioRango = new DateTime(anio.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            finRango = new DateTime(anio.Value, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999);
        }

        var queryMovimientos = _db.Movimientos.AsNoTracking().Where(m => m.IdHogar == idHogar);
        var queryAportes = _db.Aportes.AsNoTracking().Where(a => a.IdHogar == idHogar);

        if (!usarRangoFechas && anio.HasValue && mes.HasValue)
        {
            // Comportamiento histórico del filtro por mes, sin cambios.
            queryMovimientos = queryMovimientos
                .Where(m => m.Fecha.Year == anio.Value && m.Fecha.Month == mes.Value);
            queryAportes = queryAportes
                .Where(a => a.Fecha.Year == anio.Value && a.Fecha.Month == mes.Value);
        }
        else
        {
            if (inicioRango.HasValue)
            {
                var desde = inicioRango.Value;
                queryMovimientos = queryMovimientos.Where(m => m.Fecha >= desde);
                queryAportes = queryAportes.Where(a => a.Fecha >= desde);
            }
            if (finRango.HasValue)
            {
                var hasta = finRango.Value;
                queryMovimientos = queryMovimientos.Where(m => m.Fecha <= hasta);
                queryAportes = queryAportes.Where(a => a.Fecha <= hasta);
            }
        }

        var resumen = await queryMovimientos
            .GroupBy(_ => 1)
            .Select(grupo => new
            {
                TotalIngresos = grupo
                    .Where(m => m.Tipo == "Ingreso")
                    .Sum(m => (decimal?)m.Monto) ?? 0m,
                TotalGastos = grupo
                    .Where(m => m.Tipo == "Gasto")
                    .Sum(m => (decimal?)m.Monto) ?? 0m
            })
            .SingleOrDefaultAsync();

        var totalIngresos = resumen?.TotalIngresos ?? 0m;
        var totalGastos = resumen?.TotalGastos ?? 0m;

        var ingresosSalario = await queryMovimientos
            .Where(m => m.Tipo == "Ingreso" && m.Categoria.Nombre == "Salario")
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;

        var totalAportes = await queryAportes.SumAsync(a => (decimal?)a.Monto) ?? 0m;

        // Unificación del ahorro: el total es la suma de aportes (AporteMeta) del período
        // más los movimientos de tipo "Ahorro" hechos a mano. Los movimientos generados por
        // un aporte llevan la marca "aporte:{id}" en OrigenEmisora y ya están contados en
        // totalAportes, así que se excluyen aquí para no duplicarlos.
        var totalAhorroMovimientos = await queryMovimientos
            .Where(m => m.Tipo == "Ahorro"
                && (m.OrigenEmisora == null || !m.OrigenEmisora.StartsWith("aporte:")))
            .SumAsync(m => (decimal?)m.Monto) ?? 0m;
        var totalAhorro = totalAportes + totalAhorroMovimientos;

        // El GroupBy sobre la navegación MetaAhorro no es traducible por EF,
        // así que se materializa primero y se agrupa en memoria.
        var aportesDetalle = await queryAportes
            .Select(a => new { a.IdMeta, a.MetaAhorro.Titulo, a.Monto })
            .ToListAsync();

        var aportesPorMeta = aportesDetalle
            .GroupBy(a => new { a.IdMeta, a.Titulo })
            .Select(g => new AporteMetaResumenDTO(g.Key.IdMeta, g.Key.Titulo, g.Sum(a => a.Monto)))
            .Where(x => x.Monto > 0)
            .OrderByDescending(x => x.Monto)
            .ToList();

        var hayMovimientos = await queryMovimientos.AnyAsync();
        var hayAportes = await queryAportes.AnyAsync();

        return Ok(new ResumenDashboardDTO(totalIngresos, totalGastos, totalAhorro, totalIngresos - totalGastos - totalAhorro)
        {
            TieneDatos = hayMovimientos || hayAportes,
            IngresosSalario = ingresosSalario,
            AportesPorMeta = aportesPorMeta
        });
    }
}
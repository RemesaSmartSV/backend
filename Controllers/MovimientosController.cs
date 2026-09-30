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

    public MovimientosController(ApplicationDbContext db) => _db = db;

    /// <summary>Obtiene los movimientos del hogar con filtros opcionales y paginación.</summary>
    /// <param name="categoriaId">Filtra por identificador de categoría cuando se especifica.</param>
    /// <param name="tipo">Filtra por tipo de movimiento cuando se especifica.</param>
    /// <param name="fechaInicio">Filtra los movimientos desde esta fecha inclusive.</param>
    /// <param name="fechaFin">Filtra los movimientos hasta esta fecha inclusive.</param>
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
            return BadRequest(new { message = "El tipo debe ser Ingreso o Gasto." });

        var idHogar = User.GetIdHogar();
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == movimiento.IdCategoria && c.IdHogar == idHogar);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });

        movimiento.IdMovimiento = 0;
        movimiento.IdHogar = idHogar;
        movimiento.IdUsuario = User.GetIdUsuario();
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMovimiento), new { id = movimiento.IdMovimiento }, movimiento);
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
            return BadRequest(new { message = "El tipo debe ser Ingreso o Gasto." });

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
           string.Equals(tipo, "Gasto", StringComparison.Ordinal);

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

    /// <summary>Obtiene el resumen de ingresos, gastos y balance del hogar.</summary>
    /// <param name="anio">Año por el que se filtrará el resumen, si se especifica junto con el mes.</param>
    /// <param name="mes">Mes por el que se filtrará el resumen, si se especifica junto con el año.</param>
    /// <response code="200">Devuelve los totales de ingresos, gastos y balance.</response>
    [ProducesResponseType(typeof(ResumenDashboardDTO), StatusCodes.Status200OK)]
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenDashboardDTO>> GetResumen([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Movimientos.Where(m => m.IdHogar == idHogar);

        if (anio.HasValue && mes.HasValue)
            query = query.Where(m => m.Fecha.Year == anio.Value && m.Fecha.Month == mes.Value);

        var resumen = await query
            .AsNoTracking()
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

        return Ok(new ResumenDashboardDTO(totalIngresos, totalGastos, totalIngresos - totalGastos));
    }
}
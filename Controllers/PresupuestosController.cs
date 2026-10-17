using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PresupuestosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PresupuestosController(ApplicationDbContext db) => _db = db;

    /// <summary>Obtiene los presupuestos del hogar, con filtro opcional por mes y año.</summary>
    /// <param name="anio">Año de los presupuestos que se consultarán.</param>
    /// <param name="mes">Mes de los presupuestos que se consultarán.</param>
    /// <response code="200">Devuelve los presupuestos que coinciden con el filtro.</response>
    [ProducesResponseType(typeof(IEnumerable<PresupuestoResponseDTO>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PresupuestoResponseDTO>>> GetPresupuestos([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Presupuestos.Where(p => p.IdHogar == idHogar);
        if (anio.HasValue && mes.HasValue)
            query = query.Where(p => p.MesAnio.Year == anio.Value && p.MesAnio.Month == mes.Value);
        var presupuestos = await query
            .AsNoTracking()
            .OrderByDescending(p => p.MesAnio)
            .ToListAsync();

        if (presupuestos.Count == 0)
            return Ok(Array.Empty<PresupuestoResponseDTO>());

        var primerMes = presupuestos.Min(p => p.MesAnio);
        var ultimoMes = presupuestos.Max(p => p.MesAnio);
        var periodoInicio = new DateTime(primerMes.Year, primerMes.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodoFin = new DateTime(ultimoMes.Year, ultimoMes.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var categorias = presupuestos.Select(p => p.IdCategoria).Distinct().ToArray();

        var gastosPorCategoriaMes = await _db.Movimientos
            .AsNoTracking()
            .Where(m => m.IdHogar == idHogar
                     && categorias.Contains(m.IdCategoria)
                     && m.Tipo == "Gasto"
                     && m.Fecha >= periodoInicio
                     && m.Fecha < periodoFin)
            .GroupBy(m => new { m.IdCategoria, Anio = m.Fecha.Year, Mes = m.Fecha.Month })
            .Select(g => new
            {
                g.Key.IdCategoria,
                g.Key.Anio,
                g.Key.Mes,
                MontoGastado = g.Sum(m => m.Monto)
            })
            .ToDictionaryAsync(
                g => new { g.IdCategoria, g.Anio, g.Mes },
                g => g.MontoGastado);

        var respuesta = presupuestos.Select(p =>
        {
            var montoGastado = gastosPorCategoriaMes.GetValueOrDefault(new
            {
                p.IdCategoria,
                Anio = p.MesAnio.Year,
                Mes = p.MesAnio.Month
            });
            var porcentaje = p.MontoLimite == 0
                ? 0m
                : Math.Round(montoGastado / p.MontoLimite * 100m, 2);

            return new PresupuestoResponseDTO(
                p.IdPresupuesto,
                p.IdHogar,
                p.IdCategoria,
                p.MontoLimite,
                p.MesAnio,
                montoGastado,
                porcentaje,
                montoGastado > p.MontoLimite);
        });

        return Ok(respuesta);
    }

    /// <summary>Obtiene un presupuesto del hogar por su identificador.</summary>
    /// <param name="id">Identificador del presupuesto.</param>
    /// <response code="200">Devuelve el presupuesto encontrado.</response>
    /// <response code="404">El presupuesto no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(Presupuesto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<ActionResult<Presupuesto>> GetPresupuesto(int id)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.IdPresupuesto == id && p.IdHogar == User.GetIdHogar());
        return presupuesto is null ? NotFound() : Ok(presupuesto);
    }

    /// <summary>Crea un presupuesto para el hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "montoLimite": 350.00, "mesAnio": "2026-09-01T00:00:00Z"}</c></remarks>
    /// <param name="presupuesto">Datos del presupuesto que se creará.</param>
    /// <response code="201">El presupuesto fue creado.</response>
    /// <response code="400">La categoría no existe o no pertenece al hogar.</response>
    /// <response code="409">Ya existe un presupuesto para esa categoría en ese mes.</response>
    [ProducesResponseType(typeof(Presupuesto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<ActionResult<Presupuesto>> Create([FromBody] Presupuesto presupuesto)
    {
        var idHogar = User.GetIdHogar();
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == presupuesto.IdCategoria && c.IdHogar == idHogar);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });

        // Un presupuesto por hogar, categoría y mes. El índice unique de la BD
        // respalda este chequeo ante dobles submits concurrentes.
        var duplicado = await _db.Presupuestos.AnyAsync(p =>
            p.IdHogar == idHogar
            && p.IdCategoria == presupuesto.IdCategoria
            && p.MesAnio == presupuesto.MesAnio);
        if (duplicado)
            return Conflict(new { message = "Ya existe un presupuesto para esa categoría en ese mes." });

        presupuesto.IdPresupuesto = 0;
        presupuesto.IdHogar = idHogar;
        _db.Presupuestos.Add(presupuesto);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPresupuesto), new { id = presupuesto.IdPresupuesto }, presupuesto);
    }

    /// <summary>Actualiza un presupuesto del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "montoLimite": 350.00, "mesAnio": "2026-09-01T00:00:00Z"}</c></remarks>
    /// <param name="id">Identificador del presupuesto.</param>
    /// <param name="input">Nuevos datos del presupuesto.</param>
    /// <response code="204">El presupuesto fue actualizado.</response>
    /// <response code="400">La categoría no existe o no pertenece al hogar.</response>
    /// <response code="404">El presupuesto no existe o no pertenece al hogar.</response>
    /// <response code="409">Ya existe un presupuesto para esa categoría en ese mes.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Presupuesto input)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.IdPresupuesto == id && p.IdHogar == User.GetIdHogar());
        if (presupuesto is null)
            return NotFound();

        // Clave única original (hogar, categoría, mes) antes de mutar la entidad.
        var categoriaOriginal = presupuesto.IdCategoria;
        var mesOriginal = presupuesto.MesAnio;

        if (input.IdCategoria != presupuesto.IdCategoria)
        {
            var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == input.IdCategoria && c.IdHogar == User.GetIdHogar());
            if (categoria is null)
                return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });
            presupuesto.IdCategoria = input.IdCategoria;
        }

        // Cambiar de categoría o de mes mueve la clave única (hogar, categoría, mes):
        // hay que descartar que ya exista otro presupuesto con esa combinación.
        if (presupuesto.IdCategoria != categoriaOriginal || input.MesAnio != mesOriginal)
        {
            var duplicado = await _db.Presupuestos.AnyAsync(p =>
                p.IdPresupuesto != id
                && p.IdHogar == User.GetIdHogar()
                && p.IdCategoria == input.IdCategoria
                && p.MesAnio == input.MesAnio);
            if (duplicado)
                return Conflict(new { message = "Ya existe un presupuesto para esa categoría en ese mes." });
        }

        presupuesto.MontoLimite = input.MontoLimite;
        presupuesto.MesAnio = input.MesAnio;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Elimina un presupuesto del hogar.</summary>
    /// <param name="id">Identificador del presupuesto.</param>
    /// <response code="204">El presupuesto fue eliminado.</response>
    /// <response code="404">El presupuesto no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.IdPresupuesto == id && p.IdHogar == User.GetIdHogar());
        if (presupuesto is null)
            return NotFound();
        _db.Presupuestos.Remove(presupuesto);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
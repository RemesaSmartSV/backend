using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

using RemesaSmartSV.DTOs;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetasAhorroController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public MetasAhorroController(ApplicationDbContext db) => _db = db;

    /// <summary>Obtiene las metas de ahorro del hogar en páginas.</summary>
    /// <param name="page">Número de página que se consultará.</param>
    /// <param name="pageSize">Cantidad de metas por página, entre 1 y 100.</param>
    /// <response code="200">Devuelve la página solicitada de metas.</response>
    /// <response code="400">Los valores de paginación están fuera de los límites permitidos.</response>
    [ProducesResponseType(typeof(PaginatedResponse<MetaAhorro>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<MetaAhorro>>> GetMetas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page debe ser mayor o igual a 1 y pageSize debe estar entre 1 y 100." });

        var idHogar = User.GetIdHogar();
        var query = _db.MetasAhorro.Where(m => m.IdHogar == idHogar);

        var total = await query.AsNoTracking().CountAsync();
        var items = await query
            .AsNoTracking()
            .OrderBy(m => m.FechaLimite)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<MetaAhorro>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>Obtiene una meta de ahorro del hogar por su identificador.</summary>
    /// <param name="id">Identificador de la meta de ahorro.</param>
    /// <response code="200">Devuelve la meta encontrada.</response>
    /// <response code="404">La meta no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(MetaAhorro), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<ActionResult<MetaAhorro>> GetMeta(int id)
    {
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == id && m.IdHogar == User.GetIdHogar());
        return meta is null ? NotFound() : Ok(meta);
    }

    /// <summary>Crea una meta de ahorro para el hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"titulo": "Fondo de emergencia", "montoObjetivo": 1000.00, "montoActual": 0, "fechaLimite": "2027-06-30T00:00:00Z", "estado": "En progreso"}</c></remarks>
    /// <param name="meta">Datos de la meta de ahorro que se creará.</param>
    /// <response code="201">La meta de ahorro fue creada.</response>
    [ProducesResponseType(typeof(MetaAhorro), StatusCodes.Status201Created)]
    [HttpPost]
    public async Task<ActionResult<MetaAhorro>> Create([FromBody] MetaAhorro meta)
    {
        meta.IdMeta = 0;
        meta.IdHogar = User.GetIdHogar();
        meta.MontoActual = 0;
        meta.Estado = "En progreso";
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMeta), new { id = meta.IdMeta }, meta);
    }

    /// <summary>Actualiza una meta de ahorro del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"titulo": "Fondo de emergencia", "montoObjetivo": 1000.00, "montoActual": 0, "fechaLimite": "2027-06-30T00:00:00Z", "estado": "En progreso"}</c></remarks>
    /// <param name="id">Identificador de la meta de ahorro.</param>
    /// <param name="input">Nuevos datos de la meta.</param>
    /// <response code="204">La meta de ahorro fue actualizada.</response>
    /// <response code="400">El estado indicado no es válido.</response>
    /// <response code="404">La meta no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] MetaAhorro input)
    {
        if (!string.IsNullOrWhiteSpace(input.Estado) && !EsEstadoValido(input.Estado))
            return BadRequest(new { message = "El estado debe ser En progreso o Completada." });

        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == id && m.IdHogar == User.GetIdHogar());
        if (meta is null)
            return NotFound();
        meta.Titulo = input.Titulo;
        meta.MontoObjetivo = input.MontoObjetivo;
        meta.FechaLimite = input.FechaLimite;
        if (!string.IsNullOrWhiteSpace(input.Estado))
            meta.Estado = input.Estado;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool EsEstadoValido(string estado)
        => string.Equals(estado, "En progreso", StringComparison.Ordinal) ||
           string.Equals(estado, "Completada", StringComparison.Ordinal);

    /// <summary>Elimina una meta de ahorro del hogar.</summary>
    /// <param name="id">Identificador de la meta de ahorro.</param>
    /// <response code="204">La meta de ahorro fue eliminada.</response>
    /// <response code="404">La meta no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == id && m.IdHogar == User.GetIdHogar());
        if (meta is null)
            return NotFound();
        _db.MetasAhorro.Remove(meta);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
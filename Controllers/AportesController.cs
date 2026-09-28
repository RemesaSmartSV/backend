using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AportesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AportesController(ApplicationDbContext db) => _db = db;

    /// <summary>Obtiene los aportes asociados a una meta de ahorro del hogar.</summary>
    /// <param name="metaId">Identificador de la meta de ahorro.</param>
    /// <response code="200">Devuelve la lista de aportes de la meta.</response>
    /// <response code="400">La meta no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(IEnumerable<AporteMeta>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AporteMeta>>> GetAportes([FromQuery] int metaId)
    {
        var idHogar = User.GetIdHogar();
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == metaId && m.IdHogar == idHogar);
        if (meta is null)
            return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });
        return Ok(await _db.Aportes
            .AsNoTracking()
            .Where(a => a.IdMeta == metaId)
            .OrderByDescending(a => a.Fecha)
            .ToListAsync());
    }

    /// <summary>Registra un aporte en una meta de ahorro del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idMeta": 3, "monto": 25.50, "fecha": "2026-09-28T12:00:00Z"}</c></remarks>
    /// <param name="aporte">Datos del aporte que se registrará.</param>
    /// <response code="201">El aporte fue creado.</response>
    /// <response code="400">La meta no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(AporteMeta), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<AporteMeta>> Create([FromBody] AporteMeta aporte)
    {
        var idHogar = User.GetIdHogar();
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == aporte.IdMeta && m.IdHogar == idHogar);
        if (meta is null)
            return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });

        aporte.IdAporte = 0;
        _db.Aportes.Add(aporte);
        meta.MontoActual += aporte.Monto;
        if (meta.MontoActual >= meta.MontoObjetivo)
            meta.Estado = "Completada";
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAportes), new { metaId = aporte.IdMeta }, aporte);
    }

    /// <summary>Elimina un aporte de una meta de ahorro del hogar.</summary>
    /// <param name="id">Identificador del aporte.</param>
    /// <response code="204">El aporte fue eliminado.</response>
    /// <response code="404">No existe el aporte o su meta no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var idHogar = User.GetIdHogar();
        var aporte = await _db.Aportes.FirstOrDefaultAsync(a => a.IdAporte == id);
        if (aporte is null)
            return NotFound();
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == aporte.IdMeta && m.IdHogar == idHogar);
        if (meta is null)
            return NotFound();
        meta.MontoActual = Math.Max(0, meta.MontoActual - aporte.Monto);
        _db.Aportes.Remove(aporte);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
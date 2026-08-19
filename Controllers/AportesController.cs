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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AporteMeta>>> GetAportes([FromQuery] int metaId)
    {
        var idHogar = User.GetIdHogar();
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == metaId && m.IdHogar == idHogar);
        if (meta is null)
            return BadRequest(new { message = "La meta no existe o no pertenece a tu hogar." });
        return Ok(await _db.Aportes.Where(a => a.IdMeta == metaId).OrderByDescending(a => a.Fecha).ToListAsync());
    }

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
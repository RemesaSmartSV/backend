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
public class MetasAhorroController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public MetasAhorroController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MetaAhorro>>> GetMetas()
    {
        var idHogar = User.GetIdHogar();
        return Ok(await _db.MetasAhorro.Where(m => m.IdHogar == idHogar).OrderBy(m => m.FechaLimite).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MetaAhorro>> GetMeta(int id)
    {
        var meta = await _db.MetasAhorro.FirstOrDefaultAsync(m => m.IdMeta == id && m.IdHogar == User.GetIdHogar());
        return meta is null ? NotFound() : Ok(meta);
    }

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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] MetaAhorro input)
    {
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
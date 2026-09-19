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
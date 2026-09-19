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
public class CategoriasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoriasController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Categoria>>> GetCategorias(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page debe ser mayor o igual a 1 y pageSize debe estar entre 1 y 100." });

        var idHogar = User.GetIdHogar();
        var query = _db.Categorias.Where(c => c.IdHogar == idHogar);

        var total = await query.AsNoTracking().CountAsync();
        var items = await query
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<Categoria>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Categoria>> GetCategoria(int id)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        return categoria is null ? NotFound() : Ok(categoria);
    }

    [HttpPost]
    public async Task<ActionResult<Categoria>> Create([FromBody] Categoria categoria)
    {
        categoria.IdCategoria = 0;
        categoria.IdHogar = User.GetIdHogar();
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategoria), new { id = categoria.IdCategoria }, categoria);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Categoria input)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        if (categoria is null)
            return NotFound();
        categoria.Nombre = input.Nombre;
        categoria.Tipo = input.Tipo;
        categoria.Icono = input.Icono;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        if (categoria is null)
            return NotFound();
        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
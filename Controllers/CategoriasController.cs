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
public class CategoriasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoriasController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Categoria>>> GetCategorias()
    {
        var idHogar = User.GetIdHogar();
        return Ok(await _db.Categorias.Where(c => c.IdHogar == idHogar).OrderBy(c => c.Nombre).ToListAsync());
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
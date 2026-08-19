using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TipsFinancierosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TipsFinancierosController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<EducacionFinanciera>>> GetTips()
        => Ok(await _db.TipsFinancieros.OrderBy(t => t.Titulo).ToListAsync());

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<EducacionFinanciera>> GetTip(int id)
    {
        var tip = await _db.TipsFinancieros.FindAsync(id);
        return tip is null ? NotFound() : Ok(tip);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EducacionFinanciera>> Create([FromBody] EducacionFinanciera tip)
    {
        var categoria = await _db.Categorias.FindAsync(tip.IdCategoria);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe." });

        tip.IdTip = 0;
        _db.TipsFinancieros.Add(tip);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTip), new { id = tip.IdTip }, tip);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] EducacionFinanciera input)
    {
        var tip = await _db.TipsFinancieros.FindAsync(id);
        if (tip is null)
            return NotFound();
        tip.IdCategoria = input.IdCategoria;
        tip.Titulo = input.Titulo;
        tip.Contenido = input.Contenido;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tip = await _db.TipsFinancieros.FindAsync(id);
        if (tip is null)
            return NotFound();
        _db.TipsFinancieros.Remove(tip);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
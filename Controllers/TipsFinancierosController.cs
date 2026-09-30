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

    /// <summary>Obtiene todos los consejos financieros.</summary>
    /// <response code="200">Devuelve la lista de consejos financieros.</response>
    [ProducesResponseType(typeof(IEnumerable<EducacionFinanciera>), StatusCodes.Status200OK)]
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<EducacionFinanciera>>> GetTips()
        => Ok(await _db.TipsFinancieros
            .AsNoTracking()
            .OrderBy(t => t.Titulo)
            .ToListAsync());

    /// <summary>Obtiene un consejo financiero por su identificador.</summary>
    /// <param name="id">Identificador del consejo financiero.</param>
    /// <response code="200">Devuelve el consejo encontrado.</response>
    /// <response code="404">El consejo no existe.</response>
    [ProducesResponseType(typeof(EducacionFinanciera), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<EducacionFinanciera>> GetTip(int id)
    {
        var tip = await _db.TipsFinancieros
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTip == id);
        return tip is null ? NotFound() : Ok(tip);
    }

    /// <summary>Crea un consejo financiero.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "titulo": "Ahorro mensual", "contenido": "Define una cantidad mensual y sepárala al recibir tus ingresos."}</c></remarks>
    /// <param name="tip">Datos del consejo financiero que se creará.</param>
    /// <response code="201">El consejo financiero fue creado.</response>
    /// <response code="400">La categoría no existe.</response>
    [ProducesResponseType(typeof(EducacionFinanciera), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Actualiza un consejo financiero.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"idCategoria": 2, "titulo": "Ahorro mensual", "contenido": "Define una cantidad mensual y sepárala al recibir tus ingresos."}</c></remarks>
    /// <param name="id">Identificador del consejo financiero.</param>
    /// <param name="input">Nuevos datos del consejo.</param>
    /// <response code="204">El consejo financiero fue actualizado.</response>
    /// <response code="404">El consejo no existe.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>Elimina un consejo financiero.</summary>
    /// <param name="id">Identificador del consejo financiero.</param>
    /// <response code="204">El consejo financiero fue eliminado.</response>
    /// <response code="404">El consejo no existe.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
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
public class PresupuestosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PresupuestosController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Presupuesto>>> GetPresupuestos([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Presupuestos.Where(p => p.IdHogar == idHogar);
        if (anio.HasValue && mes.HasValue)
            query = query.Where(p => p.MesAnio.Year == anio.Value && p.MesAnio.Month == mes.Value);
        return Ok(await query.OrderByDescending(p => p.MesAnio).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Presupuesto>> GetPresupuesto(int id)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.IdPresupuesto == id && p.IdHogar == User.GetIdHogar());
        return presupuesto is null ? NotFound() : Ok(presupuesto);
    }

    [HttpPost]
    public async Task<ActionResult<Presupuesto>> Create([FromBody] Presupuesto presupuesto)
    {
        var idHogar = User.GetIdHogar();
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == presupuesto.IdCategoria && c.IdHogar == idHogar);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });

        presupuesto.IdPresupuesto = 0;
        presupuesto.IdHogar = idHogar;
        _db.Presupuestos.Add(presupuesto);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPresupuesto), new { id = presupuesto.IdPresupuesto }, presupuesto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Presupuesto input)
    {
        var presupuesto = await _db.Presupuestos.FirstOrDefaultAsync(p => p.IdPresupuesto == id && p.IdHogar == User.GetIdHogar());
        if (presupuesto is null)
            return NotFound();
        if (input.IdCategoria != presupuesto.IdCategoria)
        {
            var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == input.IdCategoria && c.IdHogar == User.GetIdHogar());
            if (categoria is null)
                return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });
            presupuesto.IdCategoria = input.IdCategoria;
        }
        presupuesto.MontoLimite = input.MontoLimite;
        presupuesto.MesAnio = input.MesAnio;
        await _db.SaveChangesAsync();
        return NoContent();
    }

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
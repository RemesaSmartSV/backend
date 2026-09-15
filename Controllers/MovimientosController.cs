using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MovimientosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public MovimientosController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Movimiento>>> GetMovimientos(
        [FromQuery] int? categoriaId,
        [FromQuery] string? tipo,
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Movimientos.Where(m => m.IdHogar == idHogar);

        if (categoriaId.HasValue)
            query = query.Where(m => m.IdCategoria == categoriaId.Value);
        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(m => m.Tipo == tipo);
        if (fechaInicio.HasValue)
            query = query.Where(m => m.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)
            query = query.Where(m => m.Fecha <= fechaFin.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<Movimiento>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Movimiento>> GetMovimiento(int id)
    {
        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        return movimiento is null ? NotFound() : Ok(movimiento);
    }

    [HttpPost]
    public async Task<ActionResult<Movimiento>> Create([FromBody] Movimiento movimiento)
    {
        var idHogar = User.GetIdHogar();
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == movimiento.IdCategoria && c.IdHogar == idHogar);
        if (categoria is null)
            return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });

        movimiento.IdMovimiento = 0;
        movimiento.IdHogar = idHogar;
        movimiento.IdUsuario = User.GetIdUsuario();
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMovimiento), new { id = movimiento.IdMovimiento }, movimiento);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Movimiento input)
    {
        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        if (movimiento is null)
            return NotFound();

        if (input.IdCategoria != movimiento.IdCategoria)
        {
            var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == input.IdCategoria && c.IdHogar == User.GetIdHogar());
            if (categoria is null)
                return BadRequest(new { message = "La categoría no existe o no pertenece a tu hogar." });
            movimiento.IdCategoria = input.IdCategoria;
        }

        movimiento.Monto = input.Monto;
        movimiento.Fecha = input.Fecha;
        movimiento.Tipo = input.Tipo;
        movimiento.Descripcion = input.Descripcion;
        movimiento.OrigenEmisora = input.OrigenEmisora;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var movimiento = await _db.Movimientos.FirstOrDefaultAsync(m => m.IdMovimiento == id && m.IdHogar == User.GetIdHogar());
        if (movimiento is null)
            return NotFound();
        _db.Movimientos.Remove(movimiento);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenDashboardDTO>> GetResumen([FromQuery] int? anio, [FromQuery] int? mes)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Movimientos.Where(m => m.IdHogar == idHogar);

        if (anio.HasValue && mes.HasValue)
            query = query.Where(m => m.Fecha.Year == anio.Value && m.Fecha.Month == mes.Value);

        var totalIngresos = await query
            .Where(m => m.Tipo == "Ingreso")
            .SumAsync(m => m.Monto);

        var totalGastos = await query
            .Where(m => m.Tipo == "Gasto")
            .SumAsync(m => m.Monto);

        return Ok(new ResumenDashboardDTO(totalIngresos, totalGastos, totalIngresos - totalGastos));
    }
}
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
public class HogaresController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public HogaresController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<Hogar>> GetMiHogar()
    {
        var hogar = await _db.Hogares.FindAsync(User.GetIdHogar());
        if (hogar is null)
            return NotFound();
        return Ok(hogar);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHogarRequest request)
    {
        var hogar = await _db.Hogares.FindAsync(id);
        if (hogar is null || hogar.IdHogar != User.GetIdHogar())
            return NotFound();
        hogar.NombreFamiliar = request.NombreFamiliar;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var hogar = await _db.Hogares.FindAsync(id);
        if (hogar is null || hogar.IdHogar != User.GetIdHogar())
            return NotFound();
        _db.Hogares.Remove(hogar);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
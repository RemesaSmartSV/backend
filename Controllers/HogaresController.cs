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

    /// <summary>Obtiene el hogar asociado al usuario actual.</summary>
    /// <response code="200">Devuelve el hogar del usuario.</response>
    /// <response code="404">No se encontró el hogar.</response>
    [ProducesResponseType(typeof(Hogar), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet]
    public async Task<ActionResult<Hogar>> GetMiHogar()
    {
        var hogar = await _db.Hogares.FindAsync(User.GetIdHogar());
        if (hogar is null)
            return NotFound();
        return Ok(hogar);
    }

    /// <summary>Actualiza el nombre familiar de un hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombreFamiliar": "Familia Rivera"}</c></remarks>
    /// <param name="id">Identificador del hogar.</param>
    /// <param name="request">Nuevo nombre familiar del hogar.</param>
    /// <response code="204">El hogar fue actualizado.</response>
    /// <response code="404">El hogar no existe o no pertenece al usuario actual.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>Elimina el hogar del usuario actual.</summary>
    /// <param name="id">Identificador del hogar.</param>
    /// <response code="204">El hogar fue eliminado.</response>
    /// <response code="404">El hogar no existe o no pertenece al usuario actual.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
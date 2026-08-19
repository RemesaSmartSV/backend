using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
public class UsuariosController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public UsuariosController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Usuario>>> GetMiembros()
    {
        var idHogar = User.GetIdHogar();
        return Ok(await _db.Usuarios.Where(u => u.IdHogar == idHogar).OrderBy(u => u.Nombre).ToListAsync());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Usuario>> AddMember([FromBody] AddMemberRequest request)
    {
        var idHogar = User.GetIdHogar();
        if (await _db.Usuarios.AnyAsync(u => u.Correo.ToLower() == request.Correo.ToLower()))
            return Conflict(new { message = "El correo ya está registrado." });

        var usuario = new Usuario
        {
            IdHogar = idHogar,
            Nombre = request.Nombre,
            Correo = request.Correo,
            Rol = string.IsNullOrWhiteSpace(request.Rol) ? "Miembro" : request.Rol,
            FechaRegistro = DateTime.UtcNow
        };
        usuario.ContrasenaHash = new PasswordHasher<Usuario>().HashPassword(usuario, request.Contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMiembros), new { id = usuario.IdUsuario }, usuario);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.IdHogar == User.GetIdHogar());
        if (usuario is null)
            return NotFound();
        if (!string.IsNullOrWhiteSpace(request.Nombre))
            usuario.Nombre = request.Nombre;
        if (!string.IsNullOrWhiteSpace(request.Rol))
            usuario.Rol = request.Rol;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.IdHogar == User.GetIdHogar());
        if (usuario is null)
            return NotFound();
        if (usuario.IdUsuario == User.GetIdUsuario())
            return BadRequest(new { message = "No puedes eliminar tu propio usuario." });
        _db.Usuarios.Remove(usuario);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
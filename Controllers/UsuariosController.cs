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

    /// <summary>Obtiene los miembros del hogar del usuario actual.</summary>
    /// <response code="200">Devuelve la lista de miembros del hogar.</response>
    [ProducesResponseType(typeof(IEnumerable<Usuario>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Usuario>>> GetMiembros()
    {
        var idHogar = User.GetIdHogar();
        return Ok(await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.IdHogar == idHogar)
            .OrderBy(u => u.Nombre)
            .ToListAsync());
    }

    /// <summary>Agrega un miembro al hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Ana Rivera", "correo": "ana.rivera@example.com", "contrasena": "ClaveSegura123", "rol": "Miembro"}</c></remarks>
    /// <param name="request">Datos del miembro que se agregará.</param>
    /// <response code="201">El miembro fue agregado.</response>
    /// <response code="400">El rol indicado no es válido.</response>
    /// <response code="409">El correo ya está registrado.</response>
    [ProducesResponseType(typeof(Usuario), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Usuario>> AddMember([FromBody] AddMemberRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Rol) && !EsRolValido(request.Rol))
            return BadRequest(new { message = "El rol debe ser Admin o Miembro." });

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

    /// <summary>Actualiza el nombre o el rol de un miembro del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Ana Rivera", "rol": "Admin"}</c></remarks>
    /// <param name="id">Identificador del miembro.</param>
    /// <param name="request">Valores que se actualizarán para el miembro.</param>
    /// <response code="204">El miembro fue actualizado.</response>
    /// <response code="400">El rol indicado no es válido.</response>
    /// <response code="404">El miembro no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Rol) && !EsRolValido(request.Rol))
            return BadRequest(new { message = "El rol debe ser Admin o Miembro." });

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

    private static bool EsRolValido(string rol)
        => string.Equals(rol, "Admin", StringComparison.Ordinal) ||
           string.Equals(rol, "Miembro", StringComparison.Ordinal);

    /// <summary>Elimina un miembro del hogar actual.</summary>
    /// <param name="id">Identificador del miembro.</param>
    /// <response code="204">El miembro fue eliminado.</response>
    /// <response code="400">El usuario actual no puede eliminarse a sí mismo.</response>
    /// <response code="404">El miembro no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
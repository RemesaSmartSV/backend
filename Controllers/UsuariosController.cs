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
    private readonly AuthService _authService;

    public UsuariosController(ApplicationDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

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

        if (!AuthService.EsCorreoGmail(request.Correo))
            return BadRequest(new { code = "gmail_required", message = "Solo se permiten cuentas con direcciones de correo @gmail.com." });

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

    /// <summary>Actualiza solo el nombre del usuario autenticado (auto-servicio).</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Ana Rivera"}</c></remarks>
    /// <param name="request">Nuevo nombre del usuario.</param>
    /// <response code="204">El perfil fue actualizado.</response>
    /// <response code="404">El usuario autenticado no existe.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("perfil")]
    public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());
        if (usuario is null)
            return NotFound();

        // Auto-servicio: el usuario edita sus datos personales y preferencias.
        // Rol, IdHogar, Correo y ContrasenaHash NO se tocan acá — requieren
        // endpoints con permisos específicos (cambio de contraseña, admin, etc.).
        usuario.Nombre = request.Nombre;
        usuario.Apellido = request.Apellido;
        usuario.NombreVisible = request.NombreVisible;
        usuario.FotoPerfil = request.FotoPerfil;
        usuario.Telefono = request.Telefono;
        usuario.IdiomaPreferido = request.IdiomaPreferido;
        usuario.TemaPreferido = request.TemaPreferido;
        await _db.SaveChangesAsync();
        return NoContent();
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

        // Protección: no degradar al último Admin a Miembro.
        if (!string.IsNullOrWhiteSpace(request.Rol)
            && request.Rol == "Miembro"
            && usuario.Rol == "Admin")
        {
            var otrosAdmins = await _db.Usuarios
                .CountAsync(u => u.IdHogar == usuario.IdHogar && u.Rol == "Admin" && u.IdUsuario != id);
            if (otrosAdmins == 0)
                return BadRequest(new { message = "No puedes quitar el rol de Admin al último administrador del hogar." });
        }

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

        // Protección: no eliminar al último Admin del hogar.
        if (usuario.Rol == "Admin")
        {
            var otrosAdmins = await _db.Usuarios
                .CountAsync(u => u.IdHogar == usuario.IdHogar && u.Rol == "Admin" && u.IdUsuario != id);
            if (otrosAdmins == 0)
                return BadRequest(new { message = "No puedes eliminar al último administrador del hogar." });
        }

        _db.Usuarios.Remove(usuario);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Cambia la contraseña del usuario autenticado.</summary>
    /// <response code="204">La contraseña fue actualizada.</response>
    /// <response code="400">La contraseña actual es incorrecta o las nuevas no coinciden.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPut("cambiar-contrasena")]
    public async Task<IActionResult> CambiarContrasena([FromBody] CambiarContrasenaRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());
        if (usuario is null)
            return NotFound();

        var hasher = new PasswordHasher<Usuario>();
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.ContrasenaHash, request.ContrasenaActual);
        if (resultado == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "La contraseña actual es incorrecta." });

        usuario.ContrasenaHash = hasher.HashPassword(usuario, request.NuevaContrasena);
        usuario.ResetToken = null;
        usuario.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Abandona la familia actual del usuario autenticado.</summary>
    /// <remarks>
    /// Un miembro normal puede salir libremente. Un Admin solo puede salir si hay otro Admin
    /// que pueda quedar a cargo (evita dejar la familia sin administración).
    /// No elimina datos financieros: solo desvincula al usuario del hogar.
    /// </remarks>
    /// <response code="200">LoginResponse refrescado sin hogar.</response>
    /// <response code="400">No perteneces a ninguna familia o eres el último Admin.</response>
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost("abandonar-familia")]
    public async Task<ActionResult<LoginResponse>> AbandonarFamilia()
    {
        var idUsuario = User.GetIdUsuario();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario is null)
            return NotFound();

        if (usuario.IdHogar is null or 0)
            return BadRequest(new { message = "No perteneces a ninguna familia." });

        // Si es Admin, verificar que quede al menos otro Admin.
        if (usuario.Rol == "Admin")
        {
            var otrosAdmins = await _db.Usuarios
                .CountAsync(u => u.IdHogar == usuario.IdHogar && u.Rol == "Admin" && u.IdUsuario != idUsuario);
            if (otrosAdmins == 0)
                return BadRequest(new { message = "Eres el último administrador. Transfiere la administración a otro miembro antes de abandonar la familia." });
        }

        usuario.IdHogar = null;
        usuario.Rol = null;
        await _db.SaveChangesAsync();

        return Ok(_authService.BuildLoginResponse(usuario));
    }
}

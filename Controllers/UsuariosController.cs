using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    [ProducesResponseType(typeof(IEnumerable<MiembroFamiliaDto>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MiembroFamiliaDto>>> GetMiembros()
    {
        var actual = await UsuarioActualAsync();
        if (actual?.IdHogar is null)
            return Forbid();

        var miembros = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.IdHogar == actual.IdHogar.Value)
            .OrderBy(u => u.Nombre)
            .Select(u => new MiembroFamiliaDto(
                u.IdUsuario, u.Nombre, u.Apellido, u.NombreVisible,
                u.Correo, u.FotoPerfil, u.Rol, u.FechaRegistro))
            .ToListAsync();
        return Ok(miembros);
    }

    /// <summary>Agrega un miembro al hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Ana Rivera", "correo": "ana.rivera@example.com", "contrasena": "ClaveSegura123", "rol": "Miembro"}</c></remarks>
    /// <param name="request">Datos del miembro que se agregará.</param>
    /// <response code="201">El miembro fue agregado.</response>
    /// <response code="400">El rol indicado no es válido.</response>
    /// <response code="409">El correo ya está registrado.</response>
    [ProducesResponseType(typeof(MiembroFamiliaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Usuario>> AddMember([FromBody] AddMemberRequest request)
    {
        var admin = await UsuarioActualAsync();
        if (admin?.Rol != "Admin" || admin.IdHogar is null)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(request.Rol) && !EsRolValido(request.Rol))
            return BadRequest(new { message = "El rol debe ser Admin o Miembro." });

        if (!AuthService.EsCorreoGmail(request.Correo))
            return BadRequest(new { code = "gmail_required", message = "Solo se permiten cuentas con direcciones de correo @gmail.com." });

        var idHogar = admin.IdHogar.Value;
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
        var response = new MiembroFamiliaDto(usuario.IdUsuario, usuario.Nombre, usuario.Apellido,
            usuario.NombreVisible, usuario.Correo, usuario.FotoPerfil, usuario.Rol, usuario.FechaRegistro);
        return CreatedAtAction(nameof(GetMiembros), new { id = usuario.IdUsuario }, response);
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

    /// <summary>Actualiza solo la preferencia de idioma del usuario autenticado.</summary>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("idioma")]
    [Authorize]
    public async Task<IActionResult> ActualizarIdioma([FromBody] ActualizarIdiomaRequest request)
    {
        var idioma = request.IdiomaPreferido.Trim().ToLowerInvariant();
        if (idioma is not ("es" or "en"))
            return BadRequest(new { message = "El idioma debe ser es o en." });

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());
        if (usuario is null)
            return NotFound();

        usuario.IdiomaPreferido = idioma;
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
        var admin = await UsuarioActualAsync();
        if (admin?.Rol != "Admin" || admin.IdHogar is null)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(request.Rol) && !EsRolValido(request.Rol))
            return BadRequest(new { message = "El rol debe ser Admin o Miembro." });

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.IdHogar == admin.IdHogar);
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
    /// <response code="409">El miembro tiene movimientos registrados que se conservan.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var admin = await UsuarioActualAsync();
        if (admin?.Rol != "Admin" || admin.IdHogar is null)
            return Forbid();

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.IdHogar == admin.IdHogar);
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

        try
        {
            _db.Usuarios.Remove(usuario);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (EsRestriccionLlaveForanea(ex))
        {
            // Requisito §6.3: la FK Movimiento.Usuario es RESTRICT, así que la base
            // de datos rechaza el borrado y los movimientos siguen intactos.
            return Conflict(new { message = "No se puede eliminar: el miembro tiene movimientos registrados. Los datos financieros se conservan." });
        }

        return NoContent();
    }

    // Una FK con DELETE RESTRICT se materializa en PostgreSQL como error 23503
    // (ForeignKeyViolation), con el PostgresException como inner exception.
    private static bool EsRestriccionLlaveForanea(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation;

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
    /// Cualquier miembro puede salirse libremente, incluido el último Admin.
    /// Si la familia queda sin miembros, el hogar y sus datos financieros se
    /// conservan (requisito §6.3 Caso A): el hogar queda vacío de personas pero
    /// mantiene movimientos, metas, aportes, presupuestos y categorías.
    /// </remarks>
    /// <response code="200">LoginResponse refrescado sin hogar.</response>
    /// <response code="400">No perteneces a ninguna familia.</response>
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

        usuario.IdHogar = null;
        usuario.Rol = null;
        await _db.SaveChangesAsync();

        // Si la familia se quedó sin miembros, el hogar NO se elimina: conserva
        // todos sus datos financieros por requisito (§6.3 Caso A). Las FK hacia
        // Hogar son RESTRICT a nivel de base de datos, así que ninguna cascada
        // destructiva puede borrarlos.
        return Ok(_authService.BuildLoginResponse(usuario));
    }

    private Task<Usuario?> UsuarioActualAsync()
        => _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());
}

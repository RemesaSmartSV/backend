using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ApplicationDbContext _db;

    public AuthController(AuthService auth, ApplicationDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    /// <summary>Registra un usuario sin hogar. El hogar se crea después con POST api/hogares.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Ana Rivera", "correo": "ana.rivera@example.com", "contrasena": "ClaveSegura123", "confirmarContrasena": "ClaveSegura123"}</c></remarks>
    /// <param name="request">Datos de registro del usuario.</param>
    /// <response code="200">Registro exitoso. Rol e IdHogar vienen en null hasta que el usuario tenga hogar.</response>
    /// <response code="400">Las contraseñas no coinciden o el cuerpo es inválido.</response>
    /// <response code="409">El correo ya está registrado.</response>
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!string.Equals(request.Contrasena, request.ConfirmarContrasena, StringComparison.Ordinal))
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        var result = await _auth.RegisterAsync(request);
        if (result is null)
            return Conflict(new { message = "El correo ya está registrado." });
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (result is null)
            return Unauthorized(new { message = "Credenciales incorrectas." });
        return Ok(result);
    }

    /// <summary>Solicita un restablecimiento de contraseña. Responde siempre con mensaje genérico.</summary>
    [HttpPost("solicitar-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> SolicitarReset([FromBody] SolicitarResetRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == request.Correo.ToLower());
        if (usuario is not null)
        {
            var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            usuario.ResetToken = token;
            usuario.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            // TODO Fase9: enviar correo con el token. Por ahora se loguea solo en desarrollo.
            Console.WriteLine($"[RESET] Token para {usuario.Correo}: {token}");
        }

        // Respuesta genérica: no revela si la cuenta existe.
        return Ok(new { message = "Si el correo existe, recibirás un enlace para restablecer tu contraseña." });
    }

    /// <summary>Restablece la contraseña con un token válido.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetContrasenaRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
            u.ResetToken == request.Token && u.ResetTokenExpiry > DateTime.UtcNow);

        if (usuario is null)
            return BadRequest(new { message = "El enlace de restablecimiento no es válido o expiró." });

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Entities.Usuario>();
        usuario.ContrasenaHash = hasher.HashPassword(usuario, request.NuevaContrasena);
        usuario.ResetToken = null;
        usuario.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Contraseña restablecida correctamente." });
    }
}
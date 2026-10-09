using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

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
}
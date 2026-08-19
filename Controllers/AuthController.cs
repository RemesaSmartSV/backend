using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
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
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;
using System.Security.Cryptography;
using System.Text;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly GoogleNonceStore _googleNonces;

    public AuthController(AuthService auth, ApplicationDbContext db, IEmailService email, IConfiguration configuration, GoogleNonceStore googleNonces)
    {
        _auth = auth;
        _db = db;
        _email = email;
        _configuration = configuration;
        _googleNonces = googleNonces;
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

        if (!AuthService.EsCorreoGmail(request.Correo))
            return BadRequest(new { code = "gmail_required", message = "Solo se permiten cuentas con direcciones de correo @gmail.com." });

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

    /// <summary>Emite un JWT actualizado a partir de la membresía real del usuario.</summary>
    [HttpPost("refrescar-sesion")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> RefrescarSesion()
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());
        if (usuario is null)
            return Unauthorized();
        return Ok(_auth.BuildLoginResponse(usuario));
    }

    /// <summary>Solicita un restablecimiento de contraseña. Responde siempre con mensaje genérico.</summary>
    [HttpPost("solicitar-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> SolicitarReset([FromBody] SolicitarResetRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == request.Correo.ToLower());
        if (usuario is not null)
        {
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            // Solo se almacena el hash del bearer token; el valor original se entrega una vez por email.
            usuario.ResetToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            usuario.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            _ = _email.EnviarResetContrasenaAsync(usuario.Correo, token);
        }

        // Respuesta genérica: no revela si la cuenta existe.
        return Ok(new { message = "Si el correo existe, recibirás un enlace para restablecer tu contraseña." });
    }

    /// <summary>Restablece la contraseña con un token válido.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetContrasenaRequest request)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
            u.ResetToken == tokenHash && u.ResetTokenExpiry > DateTime.UtcNow);

        if (usuario is null)
            return BadRequest(new { message = "El enlace de restablecimiento no es válido o expiró." });

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Entities.Usuario>();
        usuario.ContrasenaHash = hasher.HashPassword(usuario, request.NuevaContrasena);
        usuario.ResetToken = null;
        usuario.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Contraseña restablecida correctamente." });
    }

    /// <summary>Autenticación con Google. Valida el ID token y crea o vincula la cuenta.</summary>
    /// <remarks>Requiere GOOGLE_CLIENT_ID en variables de entorno.</remarks>
    [HttpGet("google-nonce")]
    [AllowAnonymous]
    public ActionResult GoogleNonce()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return Ok(new { nonce = _googleNonces.Create() });
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Google([FromBody] GoogleLoginRequest request)
    {
        var clientId = _configuration["GOOGLE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(503, new { message = "Google Sign-In no está configurado. Contacta al administrador." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId },
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch
        {
            return Unauthorized(new { message = "Token de Google inválido." });
        }

        var tokenNonce = payload.Nonce ?? string.Empty;
        var requestNonce = request.Nonce ?? string.Empty;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(tokenNonce), Encoding.UTF8.GetBytes(requestNonce))
            || !_googleNonces.TryConsume(requestNonce))
            return Unauthorized(new { message = "El desafío de autenticación de Google expiró o no es válido. Inténtalo de nuevo." });

        // Política: solo se permiten cuentas @gmail.com.
        if (string.IsNullOrWhiteSpace(payload.Email) || !payload.EmailVerified)
            return Unauthorized(new { message = "Google debe devolver una dirección de correo verificada." });
        if (string.IsNullOrWhiteSpace(payload.Subject))
            return Unauthorized(new { message = "Google no devolvió un identificador estable de cuenta." });

        if (!AuthService.EsCorreoGmail(payload.Email))
            return BadRequest(new { message = "Solo se permiten cuentas con direcciones de correo @gmail.com." });

        // El subject de Google es la identidad estable. Un correo verificado permite
        // vincular una cuenta local existente solo si coincide exactamente.
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.GoogleSubject == payload.Subject);

        if (usuario is not null && !string.Equals(usuario.Correo, payload.Email, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "La identidad de Google no coincide con el correo vinculado a esta cuenta." });

        if (usuario is null)
        {
            usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == payload.Email.ToLower());
            if (usuario is not null && usuario.GoogleSubject is not null && usuario.GoogleSubject != payload.Subject)
                return Conflict(new { message = "Esta cuenta ya está vinculada a otra identidad de Google." });
        }

        if (usuario is null)
        {
            usuario = new Entities.Usuario
            {
                Nombre = payload.Name ?? payload.Email.Split('@')[0],
                Correo = payload.Email.ToLower(),
                FechaRegistro = DateTime.UtcNow,
                GoogleSubject = payload.Subject,
            };
            var passwordCasual = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            usuario.ContrasenaHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Entities.Usuario>()
                .HashPassword(usuario, passwordCasual);
            _db.Usuarios.Add(usuario);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Un alta concurrente con el mismo subject/email no debe crear duplicados.
                _db.ChangeTracker.Clear();
                usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.GoogleSubject == payload.Subject);
                if (usuario is null)
                    return Conflict(new { message = "La cuenta ya existe. Inicia sesión con Google nuevamente." });
            }
        }
        else if (usuario.GoogleSubject is null)
        {
            // Vinculación segura: solo tras validar firma/audience y email_verified de Google,
            // y únicamente si el Gmail verificado coincide con el correo local existente.
            usuario.GoogleSubject = payload.Subject;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "Esta cuenta ya está vinculada a otra identidad de Google." });
            }
        }

        var result = _auth.BuildLoginResponse(usuario);
        return Ok(result);
    }
}

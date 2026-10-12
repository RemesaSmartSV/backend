using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Services;

public class AuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Valida que el correo termine en @gmail.com (política del proyecto).</summary>
    public static bool EsCorreoGmail(string correo)
        => !string.IsNullOrWhiteSpace(correo)
           && correo.Trim().EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (!EsCorreoGmail(request.Correo))
            return null;

        if (await _db.Usuarios.AnyAsync(u => u.Correo.ToLower() == request.Correo.ToLower()))
            return null;

        // El registro ya no crea el hogar: el usuario queda sin IdHogar y sin Rol
        // hasta que crea un hogar (POST api/hogares) o se une a uno existente.
        var usuario = new Usuario
        {
            Nombre = request.Nombre,
            Correo = request.Correo,
            FechaRegistro = DateTime.UtcNow
        };
        usuario.ContrasenaHash = new PasswordHasher<Usuario>().HashPassword(usuario, request.Contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return BuildLoginResponse(usuario);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        if (!EsCorreoGmail(request.Correo))
            return null;

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == request.Correo.ToLower());
        if (usuario is null)
            return null;

        var result = new PasswordHasher<Usuario>().VerifyHashedPassword(usuario, usuario.ContrasenaHash, request.Contrasena);
        if (result == PasswordVerificationResult.Failed)
            return null;

        return BuildLoginResponse(usuario);
    }

    // IdHogar y Rol pueden ser null: el usuario registrado aún no tiene hogar.
    // La respuesta refleja esa ausencia con null (sin centinelas 0/""), y se completa
    // cuando el usuario crea un hogar (POST api/hogares) o se une a uno existente.
    // Es public porque HogaresController necesita emitir un token refrescado tras
    // crear el hogar: el token del request todavía llega con el claim idHogar=0.
    public LoginResponse BuildLoginResponse(Usuario usuario) => new(
        Token: GenerateToken(usuario),
        IdUsuario: usuario.IdUsuario,
        Nombre: usuario.Nombre,
        Correo: usuario.Correo,
        Rol: usuario.Rol,
        IdHogar: usuario.IdHogar);

    private string GenerateToken(Usuario usuario)
    {
        // Dentro del token sí se usan centinelas: un Claim no admite valor null y
        // GetIdHogar() interpreta la ausencia como 0. El contrato público (LoginResponse)
        // maneja la ausencia de hogar con null; el claim del token sigue usando 0.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var claims = new[]
        {
            new Claim("idUsuario", usuario.IdUsuario.ToString()),
            new Claim("idHogar", (usuario.IdHogar ?? 0).ToString()),
            new Claim(ClaimTypes.Role, usuario.Rol ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Correo)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

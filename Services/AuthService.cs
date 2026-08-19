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

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Correo.ToLower() == request.Correo.ToLower()))
            return null;

        var hogar = new Hogar { NombreFamiliar = request.NombreFamiliar };
        _db.Hogares.Add(hogar);
        await _db.SaveChangesAsync();

        var usuario = new Usuario
        {
            IdHogar = hogar.IdHogar,
            Nombre = request.Nombre,
            Correo = request.Correo,
            Rol = "Admin",
            FechaRegistro = DateTime.UtcNow
        };
        usuario.ContrasenaHash = new PasswordHasher<Usuario>().HashPassword(usuario, request.Contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return BuildLoginResponse(usuario);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == request.Correo.ToLower());
        if (usuario is null)
            return null;

        var result = new PasswordHasher<Usuario>().VerifyHashedPassword(usuario, usuario.ContrasenaHash, request.Contrasena);
        if (result == PasswordVerificationResult.Failed)
            return null;

        return BuildLoginResponse(usuario);
    }

    private LoginResponse BuildLoginResponse(Usuario usuario) => new(
        Token: GenerateToken(usuario),
        IdUsuario: usuario.IdUsuario,
        Nombre: usuario.Nombre,
        Correo: usuario.Correo,
        Rol: usuario.Rol,
        IdHogar: usuario.IdHogar);

    private string GenerateToken(Usuario usuario)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var claims = new[]
        {
            new Claim("idUsuario", usuario.IdUsuario.ToString()),
            new Claim("idHogar", usuario.IdHogar.ToString()),
            new Claim(ClaimTypes.Role, usuario.Rol),
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
using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

public record RegisterRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Correo,
    [Required, MinLength(6)] string Contrasena,
    [Required] string NombreFamiliar);

public record LoginRequest(
    [Required] string Correo,
    [Required] string Contrasena);

public record LoginResponse(
    string Token,
    int IdUsuario,
    string Nombre,
    string Correo,
    string Rol,
    int IdHogar);

public record AddMemberRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Correo,
    [Required, MinLength(6)] string Contrasena,
    string? Rol);

public record UpdateHogarRequest([Required] string NombreFamiliar);

public record UpdateUsuarioRequest(string? Nombre, string? Rol);
using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

public record RegisterRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Correo,
    [Required, MinLength(6)] string Contrasena,
    // En un record posicional, Compare (solo válido en propiedades) exige el objetivo
    // explícito `property:` en su propio grupo de atributos; sin él el compilador da CS0592.
    [Required][property: Compare(nameof(RegisterRequest.Contrasena))] string ConfirmarContrasena);

public record LoginRequest(
    [Required] string Correo,
    [Required] string Contrasena);

// Rol e IdHogar son nullables: un usuario recién registrado queda sin hogar y sin rol
// hasta que crea un hogar (POST api/hogares) o se une a uno existente.
public record LoginResponse(
    string Token,
    int IdUsuario,
    string Nombre,
    string Correo,
    string? Rol,
    int? IdHogar);

public record AddMemberRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Correo,
    [Required, MinLength(6)] string Contrasena,
    string? Rol);

public record CrearHogarRequest([Required, StringLength(100)] string NombreFamiliar);

public record ActualizarPerfilRequest([Required, StringLength(100)] string Nombre);

public record UpdateHogarRequest([Required] string NombreFamiliar);

public record UpdateUsuarioRequest(string? Nombre, string? Rol);
public record ResumenDashboardDTO(decimal TotalIngresos, decimal TotalGastos, decimal Balance);
// Cuerpo de POST api/Invitaciones: correo del destinatario a invitar.
public record InvitarRequest(
    [Required, EmailAddress] string Correo);


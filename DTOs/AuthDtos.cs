using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

// Clase (no record posicional): Compare solo funciona en propiedades, y en un
// record posicional la validación se ignora en el pipeline HTTP (500 en
// Development). Con propiedades, [Compare] se asocia directamente a la
// propiedad y el model validation de ASP.NET Core lo valida correctamente.
public class RegisterRequest
{
    [Required]
    public string Nombre { get; set; } = null!;

    [Required, EmailAddress]
    public string Correo { get; set; } = null!;

    [Required, MinLength(6)]
    public string Contrasena { get; set; } = null!;

    [Required, Compare(nameof(Contrasena))]
    public string ConfirmarContrasena { get; set; } = null!;
}

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


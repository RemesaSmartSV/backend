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
public record ResumenDashboardDTO(decimal TotalIngresos, decimal TotalGastos, decimal Balance);

public record AlertaPeriodoRequestDTO
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int UsuarioId { get; set; }
}

public record AlertaResponseDTO(string TipoAlerta, string Mensaje, decimal PorcentajeUsado);
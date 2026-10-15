using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

/// <summary>Datos mínimos y públicos de una familia encontrada por código.</summary>
public record HogarBusquedaDto(string CodigoPublico, string NombreFamiliar);

/// <summary>Datos mínimos del miembro expuestos a usuarios autorizados de la familia.</summary>
public record MiembroFamiliaDto(
    int IdUsuario,
    string Nombre,
    string? Apellido,
    string? NombreVisible,
    string Correo,
    string? FotoPerfil,
    string? Rol,
    DateTime FechaRegistro);

public class CrearSolicitudUnionRequest
{
    [Required, StringLength(23, MinimumLength = 23)]
    public string CodigoPublico { get; set; } = null!;
}

public record SolicitudUnionPropiaDto(
    int IdSolicitud,
    string CodigoPublico,
    string NombreFamilia,
    string Estado,
    DateTime FechaSolicitud,
    DateTime? FechaRespuesta);

public record SolicitudUnionAdminDto(
    int IdSolicitud,
    string NombreSolicitante,
    string CorreoSolicitante,
    string Estado,
    DateTime FechaSolicitud);

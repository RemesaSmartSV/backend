using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

/// <summary>Datos mínimos y públicos de una familia encontrada por código.</summary>
public record HogarBusquedaDto(string CodigoPublico, string NombreFamiliar);

/// <summary>Familia encontrada por nombre. Nunca expone el código público: la búsqueda
/// por nombre solo permite solicitar unión (que el Admin aprueba), no unirse directo.</summary>
public record HogarResultadoBusquedaDto(int IdHogar, string NombreFamiliar);

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
    /// <summary>Código público de la familia. Debe venir solo o, alternativamente, con IdHogar; nunca ambos.</summary>
    [StringLength(23)]
    public string? CodigoPublico { get; set; }

    /// <summary>Identificador de la familia (p. ej. obtenido de la búsqueda por nombre, que no expone el código).
    /// Debe venir solo o, alternativamente, con CodigoPublico; nunca ambos.</summary>
    public int? IdHogar { get; set; }
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

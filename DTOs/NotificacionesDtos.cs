using RemesaSmartSV.Entities;

namespace RemesaSmartSV.DTOs;

// Pagina de notificaciones del usuario: incluye el contador de no leidas para el
// badge de la campana sin una segunda consulta.
public record NotificacionesResponseDto(
    IReadOnlyList<Notificacion> Items,
    int Total,
    int SinLeer,
    int Page,
    int PageSize);

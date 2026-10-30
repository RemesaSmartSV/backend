namespace RemesaSmartSV.DTOs;

/// <summary>
/// Superset aditivo de la entidad Movimiento para GET /api/movimientos:
/// conserva campo por campo el contrato crudo actual y agrega NombreAutor,
/// resuelto desde el Usuario relacionado. Funciona para miembros removidos
/// del hogar porque su fila usuario persiste con IdHogar = null.
/// </summary>
public record MovimientoListadoDto(
    int IdMovimiento,
    int IdHogar,
    int IdUsuario,
    int IdCategoria,
    decimal Monto,
    DateTime Fecha,
    string Tipo,
    string? Descripcion,
    string? OrigenEmisora,
    string? NombreAutor);

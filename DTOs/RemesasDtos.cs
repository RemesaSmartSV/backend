namespace RemesaSmartSV.DTOs;

/// <summary>Resumen anual del ledger de remesas de un hogar.</summary>
public record ResumenRemesasDTO(
    int Anio,
    decimal TotalEnviado,
    decimal TotalRecibido,
    decimal TotalComision,
    int Cantidad,
    List<ResumenRemesaCanalDTO> PorCanal);

/// <summary>Totales de remesas de un canal dentro del resumen anual.</summary>
public record ResumenRemesaCanalDTO(
    string Canal,
    int Cantidad,
    decimal MontoEnviado,
    decimal MontoRecibido,
    decimal Comision);

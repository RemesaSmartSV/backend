namespace RemesaSmartSV.DTOs;

public record PresupuestoResponseDTO(
    int IdPresupuesto,
    int IdHogar,
    int IdCategoria,
    decimal MontoLimite,
    DateTime MesAnio,
    decimal MontoGastado,
    decimal Porcentaje,
    bool Excedido);

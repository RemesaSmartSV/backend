namespace RemesaSmartSV.DTOs;

public record ReportePeriodoRequestDTO
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
}

public record ReportePeriodoResponseDTO(
    DateTime FechaInicio,
    DateTime FechaFin,
    decimal TotalIngresos,
    decimal TotalGastos,
    decimal Balance,
    int CantidadIngresos,
    int CantidadGastos);
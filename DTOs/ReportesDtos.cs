using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

public record ReportePeriodoRequestDTO
{
    [Required] public DateTime FechaInicio { get; set; }
    [Required] public DateTime FechaFin { get; set; }
}

public record ReportePeriodoResponseDTO(
    DateTime FechaInicio,
    DateTime FechaFin,
    decimal TotalIngresos,
    decimal TotalGastos,
    decimal Balance,
    int CantidadIngresos,
    int CantidadGastos);

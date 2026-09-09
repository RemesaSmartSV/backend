using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

public record AlertaPeriodoRequestDTO
{
    [Required] public DateTime FechaInicio { get; set; }
    [Required] public DateTime FechaFin { get; set; }
}

public record AlertaResponseDTO(string TipoAlerta, string Mensaje, decimal PorcentajeUsado);

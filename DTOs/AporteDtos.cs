using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

/// <summary>Datos editables por el cliente al registrar aporte; hogar/usuario siempre vienen del servidor.</summary>
public class CrearAporteRequest
{
    [Range(1, int.MaxValue)]
    public int IdMeta { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Monto { get; set; }

    public DateTime? Fecha { get; set; }

    [StringLength(255)]
    public string? Notas { get; set; }

    public Guid IdempotencyKey { get; set; }
}

public record AporteResponseDto(
    int IdAporte,
    int IdMeta,
    decimal Monto,
    DateTime Fecha,
    string? Notas);

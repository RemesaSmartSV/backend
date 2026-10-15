using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class SolicitudUnion
{
    public const string Pendiente = "Pendiente";
    public const string Aceptada = "Aceptada";
    public const string Rechazada = "Rechazada";
    public const string Cancelada = "Cancelada";

    [Key]
    public int IdSolicitud { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    public int IdUsuario { get; set; }

    [Required]
    [StringLength(20)]
    [AllowedValues(Pendiente, Aceptada, Rechazada, Cancelada)]
    public string Estado { get; set; } = Pendiente;

    [Required]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public DateTime? FechaRespuesta { get; set; }

    public int? IdAdminRespuesta { get; set; }

    [ForeignKey(nameof(IdHogar))]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey(nameof(IdUsuario))]
    [JsonIgnore]
    public virtual Usuario Usuario { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class InvitacionFamilia
{
    public const string EstadoPendiente = "Pendiente";
    public const string EstadoAceptada = "Aceptada";
    public const string EstadoRechazada = "Rechazada";

    [Key]
    public int IdInvitacion { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    public int IdInvitador { get; set; }

    public int? IdInvitado { get; set; }

    [Required]
    [StringLength(150)]
    public string Correo { get; set; } = null!;

    [Required]
    [StringLength(20)]
    [AllowedValues(EstadoPendiente, EstadoAceptada, EstadoRechazada)]
    public string Estado { get; set; } = EstadoPendiente;

    [Required]
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;

    public DateTime? FechaRespuesta { get; set; }

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdInvitador")]
    [JsonIgnore]
    public virtual Usuario Invitador { get; set; } = null!;

    [ForeignKey("IdInvitado")]
    [JsonIgnore]
    public virtual Usuario? Invitado { get; set; }
}

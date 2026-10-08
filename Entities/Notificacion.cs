using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class Notificacion
{
    public const string TipoInvitacion = "Invitacion";
    public const string TipoAlerta = "Alerta";
    public const string TipoSistema = "Sistema";

    [Key]
    public int IdNotificacion { get; set; }

    [Required]
    public int IdUsuario { get; set; }

    [Required]
    [StringLength(50)]
    public string Tipo { get; set; } = null!;

    [Required]
    public string Mensaje { get; set; } = null!;

    public bool Leida { get; set; }

    public int? IdReferencia { get; set; }

    [Required]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [ForeignKey("IdUsuario")]
    [JsonIgnore]
    public virtual Usuario Usuario { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class MetaAhorro
{
    [Key]
    public int IdMeta { get; set; }

    [Required]
    public int IdHogar { get; set; }

    /// <summary>Usuario que creó la meta. Nullable para metas heredadas o del sistema.</summary>
    public int? IdUsuario { get; set; }

    [Required]
    [StringLength(100)]
    public string Titulo { get; set; } = null!;

    [StringLength(255)]
    public string? Descripcion { get; set; }

    [Required]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal MontoObjetivo { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal MontoActual { get; set; } = 0;

    [Required]
    public DateTime FechaLimite { get; set; }

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = "En progreso";

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    [JsonIgnore]
    public virtual Usuario? Usuario { get; set; }

    [JsonIgnore]
    public virtual ICollection<AporteMeta> Aportes { get; set; } = new List<AporteMeta>();
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RemesaSmartSV.Entities;

public class MetaAhorro
{
    [Key]
    public int IdMeta { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    [StringLength(100)]
    public string Titulo { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal MontoObjetivo { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal MontoActual { get; set; } = 0;

    [Required]
    public DateTime FechaLimite { get; set; }

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = "En progreso";

    [ForeignKey("IdHogar")]
    public virtual Hogar Hogar { get; set; } = null!;

    public virtual ICollection<AporteMeta> Aportes { get; set; } = new List<AporteMeta>();
}
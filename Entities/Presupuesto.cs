using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RemesaSmartSV.Entities;

public class Presupuesto
{
    [Key]
    public int IdPresupuesto { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    public int IdCategoria { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal MontoLimite { get; set; }

    [Required]
    public DateTime MesAnio { get; set; }

    [ForeignKey("IdHogar")]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdCategoria")]
    public virtual Categoria Categoria { get; set; } = null!;
}
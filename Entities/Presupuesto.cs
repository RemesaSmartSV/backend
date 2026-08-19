using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

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
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdCategoria")]
    [JsonIgnore]
    public virtual Categoria Categoria { get; set; } = null!;
}
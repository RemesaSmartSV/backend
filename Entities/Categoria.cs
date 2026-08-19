using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class Categoria
{
    [Key]
    public int IdCategoria { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    [StringLength(50)]
    public string Nombre { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = null!;

    [StringLength(50)]
    public string? Icono { get; set; }

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [JsonIgnore]
    public virtual ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();
}
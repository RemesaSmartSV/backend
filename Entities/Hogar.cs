using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class Hogar
{
    [Key]
    public int IdHogar { get; set; }

    [Required]
    [StringLength(100)]
    public string NombreFamiliar { get; set; } = null!;

    [Required]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    [JsonIgnore]
    public virtual ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
    [JsonIgnore]
    public virtual ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();
    [JsonIgnore]
    public virtual ICollection<MetaAhorro> MetasAhorro { get; set; } = new List<MetaAhorro>();
}
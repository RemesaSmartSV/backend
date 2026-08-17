using System.ComponentModel.DataAnnotations;

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

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public virtual ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();
    public virtual ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();
    public virtual ICollection<MetaAhorro> MetasAhorro { get; set; } = new List<MetaAhorro>();
}
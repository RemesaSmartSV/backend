using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RemesaSmartSV.Entities;

public class Movimiento
{
    [Key]
    public int IdMovimiento { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    public int IdUsuario { get; set; }

    [Required]
    public int IdCategoria { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal Monto { get; set; }

    [Required]
    public DateTime Fecha { get; set; }

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = null!;

    [StringLength(255)]
    public string? Descripcion { get; set; }

    [StringLength(100)]
    public string? OrigenEmisora { get; set; }

    [ForeignKey("IdHogar")]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("IdCategoria")]
    public virtual Categoria Categoria { get; set; } = null!;
}
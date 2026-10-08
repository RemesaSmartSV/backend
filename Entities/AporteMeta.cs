using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class AporteMeta
{
    [Key]
    public int IdAporte { get; set; }

    [Required]
    public int IdMeta { get; set; }

    [Required]
    public int IdHogar { get; set; }

    public int? IdUsuario { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Monto { get; set; }

    [Required]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [StringLength(255)]
    public string? Notas { get; set; }

    [ForeignKey("IdMeta")]
    [JsonIgnore]
    public virtual MetaAhorro MetaAhorro { get; set; } = null!;

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    [JsonIgnore]
    public virtual Usuario? Usuario { get; set; }
}
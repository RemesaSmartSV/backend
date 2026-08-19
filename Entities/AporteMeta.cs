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
    [Column(TypeName = "decimal(10, 2)")]
    public decimal Monto { get; set; }

    [Required]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [ForeignKey("IdMeta")]
    [JsonIgnore]
    public virtual MetaAhorro MetaAhorro { get; set; } = null!;
}
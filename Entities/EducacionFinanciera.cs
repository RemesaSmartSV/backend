using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class EducacionFinanciera
{
    [Key]
    public int IdTip { get; set; }

    [Required]
    public int IdCategoria { get; set; }

    [Required]
    [StringLength(150)]
    public string Titulo { get; set; } = null!;

    [Required]
    public string Contenido { get; set; } = null!;

    [ForeignKey("IdCategoria")]
    [JsonIgnore]
    public virtual Categoria Categoria { get; set; } = null!;
}
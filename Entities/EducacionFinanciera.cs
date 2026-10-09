using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class EducacionFinanciera
{
    [Key]
    public int IdTip { get; set; }

    /// <summary>
    /// Categoría opcional de referencia. Los tips son contenido educativo global
    /// (GET /api/TipsFinancieros es anónimo y sin filtro de hogar), mientras que las
    /// categorías son por hogar: por eso el vínculo no puede ser obligatorio.
    /// </summary>
    public int? IdCategoria { get; set; }

    [Required]
    [StringLength(150)]
    public string Titulo { get; set; } = null!;

    [Required]
    public string Contenido { get; set; } = null!;

    [ForeignKey("IdCategoria")]
    [JsonIgnore]
    public virtual Categoria? Categoria { get; set; }
}
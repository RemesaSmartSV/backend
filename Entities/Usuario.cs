using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class Usuario
{
    [Key]
    public int IdUsuario { get; set; }

    [Required]
    public int IdHogar { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Correo { get; set; } = null!;

    [Required]
    [StringLength(255)]
    [JsonIgnore]
    public string ContrasenaHash { get; set; } = null!;

    [Required]
    [StringLength(20)]
    public string Rol { get; set; } = null!;

    [Required]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;
}
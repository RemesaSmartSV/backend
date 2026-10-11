using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

public class Usuario
{
    [Key]
    public int IdUsuario { get; set; }

    public int? IdHogar { get; set; }

    [Required]
    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    [StringLength(100)]
    public string? Apellido { get; set; }

    [StringLength(100)]
    public string? NombreVisible { get; set; }

    [StringLength(500)]
    public string? FotoPerfil { get; set; }

    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(10)]
    public string? IdiomaPreferido { get; set; }

    [StringLength(20)]
    public string? TemaPreferido { get; set; }

    [StringLength(100)]
    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpiry { get; set; }

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public string Correo { get; set; } = null!;

    [Required]
    [StringLength(255)]
    [JsonIgnore]
    public string ContrasenaHash { get; set; } = null!;

    [StringLength(20)]
    public string? Rol { get; set; }

    [Required]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar? Hogar { get; set; }
}
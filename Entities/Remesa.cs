using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Entities;

/// <summary>
/// Constantes de dirección y canal del ledger de remesas,
/// al mismo estilo que <see cref="InvitacionFamilia.EstadoPendiente"/>.
/// </summary>
public static class RemesaConstantes
{
    public const string DireccionEnvio = "Envio";
    public const string DireccionRecepcion = "Recepcion";

    public const string CanalWesternUnion = "WesternUnion";
    public const string CanalGiros = "Giros";
    public const string CanalAgenteBanco = "AgenteBanco";
    public const string CanalEfectivo = "Efectivo";
    public const string CanalBilleteraDigital = "BilleteraDigital";
    public const string CanalOtro = "Otro";
}

/// <summary>
/// Registro del ledger de remesas: cada fila es un envío o una recepción
/// de remesa con su canal, montos, comisión y folio (MTCN u homólogo).
/// </summary>
public class Remesa
{
    [Key]
    public int IdRemesa { get; set; }

    [Required]
    public int IdHogar { get; set; }

    /// <summary>Miembro que registró la remesa; nullable porque el claim pudo faltar en datos históricos.</summary>
    public int? IdUsuario { get; set; }

    [Required]
    [StringLength(20)]
    [AllowedValues(RemesaConstantes.DireccionEnvio, RemesaConstantes.DireccionRecepcion)]
    public string Direccion { get; set; } = null!;

    [Required]
    [StringLength(30)]
    [AllowedValues(
        RemesaConstantes.CanalWesternUnion,
        RemesaConstantes.CanalGiros,
        RemesaConstantes.CanalAgenteBanco,
        RemesaConstantes.CanalEfectivo,
        RemesaConstantes.CanalBilleteraDigital,
        RemesaConstantes.CanalOtro)]
    public string Canal { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal MontoEnviado { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal MontoRecibido { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal Comision { get; set; }

    /// <summary>MTCN de Western Union o folio equivalente del canal utilizado.</summary>
    [StringLength(60)]
    public string? Folio { get; set; }

    [Required]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [StringLength(255)]
    public string? Notas { get; set; }

    /// <summary>Enlace entre el envío y su recepción correspondiente (conciliación futura).</summary>
    public int? IdRemesaVinculada { get; set; }

    [ForeignKey("IdHogar")]
    [JsonIgnore]
    public virtual Hogar Hogar { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    [JsonIgnore]
    public virtual Usuario? Usuario { get; set; }

    [ForeignKey("IdRemesaVinculada")]
    [JsonIgnore]
    public virtual Remesa? RemesaVinculada { get; set; }
}

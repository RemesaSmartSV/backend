using System.ComponentModel.DataAnnotations;

namespace RemesaSmartSV.DTOs;

// Clase (no record posicional): Compare solo funciona en propiedades, y en un
// record posicional la validación se ignora en el pipeline HTTP (500 en
// Development). Con propiedades, [Compare] se asocia directamente a la
// propiedad y el model validation de ASP.NET Core lo valida correctamente.
public class RegisterRequest
{
    [Required]
    public string Nombre { get; set; } = null!;

    [Required, EmailAddress]
    public string Correo { get; set; } = null!;

    [Required, MinLength(6)]
    public string Contrasena { get; set; } = null!;

    [Required, Compare(nameof(Contrasena))]
    public string ConfirmarContrasena { get; set; } = null!;
}

public record LoginRequest(
    [Required] string Correo,
    [Required] string Contrasena);

// Rol e IdHogar son nullables: un usuario recién registrado queda sin hogar y sin rol
// hasta que crea un hogar (POST api/hogares) o se une a uno existente.
public record LoginResponse(
    string Token,
    int IdUsuario,
    string Nombre,
    string Correo,
    string? Rol,
    int? IdHogar,
    string? IdiomaPreferido);

public record AddMemberRequest(
    [Required] string Nombre,
    [Required, EmailAddress] string Correo,
    [Required, MinLength(6)] string Contrasena,
    string? Rol);

public record CrearHogarRequest([Required, StringLength(100)] string NombreFamiliar);

public class ActualizarPerfilRequest
{
    [Required, StringLength(100)]
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
}

public class ActualizarIdiomaRequest
{
    [Required, StringLength(10)]
    public string IdiomaPreferido { get; set; } = null!;
}

public record UpdateHogarRequest([Required] string NombreFamiliar);

public record UpdateUsuarioRequest(string? Nombre, string? Rol);
/// <summary>
/// Desglose del ahorro por meta dentro del período consultado:
/// suma de los AporteMeta de cada meta, solo metas con monto &gt; 0,
/// ordenado por monto descendente.
/// </summary>
public record AporteMetaResumenDTO(int IdMeta, string Titulo, decimal Monto);

/// <summary>
/// Resumen del tablero. TotalAhorro suma los aportes (AporteMeta) del período
/// más los movimientos de tipo "Ahorro" no generados por un aporte (sin la
/// marca "aporte:" en OrigenEmisora), para no duplicar los que ya vienen de
/// AporteMeta. Balance = Ingresos - Gastos - Ahorro.
/// </summary>
public record ResumenDashboardDTO(
    decimal TotalIngresos,
    decimal TotalGastos,
    decimal TotalAhorro,
    decimal Balance)
{
    /// <summary>True si hay algún movimiento o algún aporte en el rango consultado.</summary>
    public bool TieneDatos { get; init; }

    /// <summary>Suma de los movimientos de tipo "Ingreso" de la categoría "Salario" en el rango.</summary>
    public decimal IngresosSalario { get; init; }

    /// <summary>Aportes por meta en el rango (solo metas con monto &gt; 0, orden desc).</summary>
    public List<AporteMetaResumenDTO> AportesPorMeta { get; init; } = new();
}
// Cuerpo de POST api/Invitaciones: correo del destinatario a invitar.
public record InvitarRequest(
    [Required, EmailAddress] string Correo);

// Cambio de contraseña (auto-servicio).
public class CambiarContrasenaRequest
{
    [Required] public string ContrasenaActual { get; set; } = null!;
    [Required, MinLength(6)] public string NuevaContrasena { get; set; } = null!;
    [Required, Compare(nameof(NuevaContrasena))] public string ConfirmarNueva { get; set; } = null!;
}

// Solicitud de restablecimiento de contraseña.
public record SolicitarResetRequest([Required, EmailAddress] string Correo);

// Restablecimiento de contraseña con token.
public class ResetContrasenaRequest
{
    [Required] public string Token { get; set; } = null!;
    [Required, MinLength(6)] public string NuevaContrasena { get; set; } = null!;
    [Required, Compare(nameof(NuevaContrasena))] public string ConfirmarNueva { get; set; } = null!;
}

// Login con Google: ID token GIS y nonce de un solo uso emitido por el backend.
public record GoogleLoginRequest([Required] string IdToken, [Required] string Nonce);


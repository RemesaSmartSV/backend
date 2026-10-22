using System.Net;
using System.Text;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas de la plantilla HTML de los correos transaccionales: capa de
/// confianza (mensaje automático y aviso anti-phishing), escape de datos
/// dinámicos contra inyección de marcado y estructura multi-cliente
/// (tablas role="presentation", meta viewport). Incluye la generación de
/// previews HTML en la carpeta temporal del usuario para revisión visual;
/// nunca escribe dentro del repositorio.
/// </summary>
public class PlantillaCorreoTests
{
    private static readonly DateTime FechaFija = new(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void MovimientoFamilia_ConDescripcionMaliciosa_SaleEscapadaEnElHtml()
    {
        const string malicioso = "<script>alert(1)</script><a href=\"evil\">x</a>";

        var (_, html, _) = SmtpEmailService.ConstruirMovimientoFamilia(
            "Ana", "Luis", "Gasto", 12.5m, malicioso, FechaFija);

        // El payload no debe existir como marcado crudo dentro del correo.
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("href=\"evil\"", html);
        // Debe aparecer en su forma codificada.
        Assert.Contains(WebUtility.HtmlEncode(malicioso), html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Plantilla_IncluyeCapaDeConfianza()
    {
        var html = SmtpEmailService.Plantilla("Título de prueba", "<p>Cuerpo de prueba</p>");

        Assert.Contains(
            "Por tu seguridad, RemesaSmart nunca te pedirá tu contraseña, códigos de verificación ni datos bancarios por correo.",
            html);
        Assert.Contains("Mensaje automático", html);
        Assert.Contains("Si no esperabas este correo, puedes ignorarlo sin problema.", html);
    }

    [Fact]
    public void Plantilla_UsaTablasDePresentacionYMetaViewport()
    {
        var html = SmtpEmailService.Plantilla("Título de prueba", "<p>Cuerpo de prueba</p>");

        Assert.Contains("role=\"presentation\"", html);
        Assert.Contains("name=\"viewport\"", html);
        Assert.Contains("x-apple-disable-message-reformatting", html);
    }

    /// <summary>
    /// Renderiza 4 correos de ejemplo con datos ficticios y los escribe en la
    /// carpeta temporal del usuario para la revisión visual del diseño. El
    /// contenido es determinista (datos fijos) y solo escribe fuera del repo.
    /// </summary>
    [Fact]
    public void GenerarPreviewsDeCorreo()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "opencode", "email-previews");
        Directory.CreateDirectory(carpeta);

        var previews = new (string Nombre, string Html)[]
        {
            ("invitacion-familiar.html",
                SmtpEmailService.ConstruirInvitacionFamiliar("Familia Pérez", "Ana", "token-preview-0001").Html),
            ("movimiento-familia.html",
                SmtpEmailService.ConstruirMovimientoFamilia("Ana", "Luis", "Gasto", 1250.75m, "Compra de alimentos para la semana", FechaFija).Html),
            ("union-familia.html",
                SmtpEmailService.ConstruirUnionFamilia("Familia Pérez", "Ana").Html),
            ("aviso-acceso.html",
                SmtpEmailService.ConstruirAvisoAcceso(FechaFija, "San Salvador, SV · Chrome en Windows").Html),
        };

        foreach (var (nombre, html) in previews)
        {
            var ruta = Path.Combine(carpeta, nombre);
            File.WriteAllText(ruta, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Assert.True(new FileInfo(ruta).Length > 0, $"El preview {nombre} quedó vacío.");
        }
    }
}

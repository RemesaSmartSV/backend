using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
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

    [Fact]
    public void Plantilla_UsaLogoEmbebidoPorCidSinImagenesExternas()
    {
        var html = SmtpEmailService.Plantilla("Título de prueba", "<p>Cuerpo de prueba</p>");

        // El logo viaja como adjunto vinculado (cid:logo); ningún host de
        // imágenes externo: los clientes de correo bloquean las URLs remotas.
        Assert.Contains("src=\"cid:logo\"", html);
        var fuentes = Regex.Matches(html, "src=\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .ToArray();
        Assert.NotEmpty(fuentes);
        Assert.All(fuentes, f => Assert.StartsWith("cid:", f));
        Assert.DoesNotContain("src=\"http", html);
    }

    [Fact]
    public void RecursoEmbebido_Logo_ExisteYEsPng()
    {
        var nombres = typeof(SmtpEmailService).Assembly.GetManifestResourceNames();
        Assert.Contains(SmtpEmailService.LogoRecurso, nombres);

        using var stream = SmtpEmailService.AbrirStreamLogo();
        Assert.NotNull(stream);

        var cabecera = new byte[4];
        Assert.Equal(4, stream.Read(cabecera, 0, 4));
        // Magic bytes PNG: 89 50 4E 47.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, cabecera);
    }

    [Fact]
    public void ConstruirMensaje_Multipart_TieneUnLogoVinculadoEnLaVistaHtml()
    {
        using var mensaje = SmtpEmailService.ConstruirMensaje(
            "remitente@prueba.invalid", "destino@prueba.invalid", "Asunto de prueba",
            "<p>Cuerpo de prueba</p>", "Cuerpo de prueba");

        Assert.Equal(2, mensaje.AlternateViews.Count);
        Assert.Equal("text/plain", mensaje.AlternateViews[0].ContentType.MediaType);
        Assert.Equal("text/html", mensaje.AlternateViews[1].ContentType.MediaType);

        var recurso = Assert.Single(mensaje.AlternateViews[1].LinkedResources);
        Assert.Equal("logo", recurso.ContentId);
        Assert.Equal("image/png", recurso.ContentType.MediaType);
        Assert.Equal(TransferEncoding.Base64, recurso.TransferEncoding);
    }

    /// <summary>
    /// Renderiza 4 correos de ejemplo con datos ficticios y los escribe en la
    /// carpeta temporal del usuario para la revisión visual del diseño. El
    /// contenido es determinista (datos fijos) y solo escribe fuera del repo.
    /// El logo se embebe como data URI porque el preview se abre en el
    /// navegador, fuera de un cliente de correo donde cid: no resuelve; el
    /// correo real usa el CID vinculado al adjunto.
    /// </summary>
    [Fact]
    public void GenerarPreviewsDeCorreo()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "opencode", "email-previews");
        Directory.CreateDirectory(carpeta);

        string logoInline;
        using (var stream = SmtpEmailService.AbrirStreamLogo())
        using (var memoria = new MemoryStream())
        {
            stream.CopyTo(memoria);
            logoInline = "data:image/png;base64," + Convert.ToBase64String(memoria.ToArray());
        }

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
            var htmlPreview = html.Replace("cid:logo", logoInline);
            Assert.Contains("data:image/png;base64,", htmlPreview);
            File.WriteAllText(ruta, htmlPreview, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Assert.True(new FileInfo(ruta).Length > 0, $"El preview {nombre} quedó vacío.");
        }
    }
}

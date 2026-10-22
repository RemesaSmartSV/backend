using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace RemesaSmartSV.Services;

/// <summary>
/// Envío de correos vía SMTP con Gmail.
/// Credenciales en variables de entorno: SMTP_USER, SMTP_PASS.
/// NOTA: Gmail requiere "App Password" (no la contraseña normal) si 2FA está activado.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger) => _logger = logger;

    private async Task<bool> EnviarAsync(string destino, string asunto, string cuerpoHtml, string cuerpoTexto)
    {
        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER");
        var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS");

        if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("SMTP no configurado (SMTP_USER/SMTP_PASS). Correo no enviado.");
            return false;
        }

        try
        {
            using var cliente = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true,
            };

            var mensaje = new MailMessage
            {
                From = new MailAddress(smtpUser, "RemesaSmart SV"),
                Subject = asunto,
                Body = cuerpoHtml,
                IsBodyHtml = true,
            };
            mensaje.To.Add(destino);
            mensaje.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(cuerpoTexto, null, "text/plain"));

            await cliente.SendMailAsync(mensaje);
            _logger.LogInformation("Correo enviado correctamente.");
            return true;
        }
        catch (Exception ex)
        {
            // No registrar destinatario, asunto, cuerpo ni credenciales: pueden
            // contener PII, enlaces con tokens o detalles del proveedor. El
            // SmtpStatusCode (enum, ej. 535) y el tipo de la inner exception sí
            // son seguros y son los que permiten diagnosticar el fallo real.
            var codigoSmtp = ex is SmtpException smtpEx ? smtpEx.StatusCode.ToString() : "n/a";
            var tipoInterna = ex.InnerException?.GetType().Name ?? "ninguna";
            _logger.LogError(
                "Error al enviar correo. Tipo de excepción: {ExceptionType}. SmtpStatusCode: {SmtpStatusCode}. Inner: {InnerExceptionType}",
                ex.GetType().Name, codigoSmtp, tipoInterna);
            return false;
        }
    }

    // -----------------------------------------------------------------
    // Construcción de los mensajes.
    // Los builders son internal para que la suite (RemesaSmartSV.Tests,
    // vía InternalsVisibleTo) pueda renderizar el HTML sin enviar correos
    // y sin exponer nada en la API pública.
    // -----------------------------------------------------------------

    /// <summary>
    /// Escapa un valor dinámico antes de interpolarlo en HTML (evita inyección
    /// de marcado dentro del propio correo).
    /// </summary>
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    /// <summary>Monto con formato invariante para el cuerpo HTML.</summary>
    private static string Monto(decimal monto) => monto.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Fecha y hora con formato invariante para el cuerpo HTML.</summary>
    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string BaseUrl() => Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";

    /// <summary>
    /// Plantilla base compartida por todos los correos. Diseño transaccional
    /// robusto multi-cliente: DOCTYPE + meta viewport / color-scheme, estructura
    /// de tablas <c>role="presentation"</c> (Outlook deforma los divs), estilos
    /// 100% inline, preheader oculto, header con marca tipográfica, tarjeta con
    /// contenido, CTA opcional y footer con capa de confianza (mensaje
    /// automático + aviso anti-phishing). Outlook ignora max-width, sombras y
    /// border-radius: se acepta degradación progresiva.
    /// Contrato de escape: <paramref name="titulo"/>, <paramref name="preheader"/>,
    /// <paramref name="textoCta"/>, <paramref name="icono"/> y
    /// <paramref name="enlaceCta"/> se reciben sin escapar y se codifican aquí;
    /// <paramref name="cuerpoHtml"/> es HTML confiable construido por los
    /// builders, con <see cref="Enc"/> sobre cada valor dinámico.
    /// </summary>
    internal static string Plantilla(
        string titulo,
        string cuerpoHtml,
        string? enlaceCta = null,
        string textoCta = "Ver más",
        string? icono = null,
        string? preheader = null)
    {
        var anio = DateTime.UtcNow.Year;
        var fuente = "'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";

        var preheaderHtml = string.IsNullOrEmpty(preheader)
            ? string.Empty
            : $"""<div style="display:none;max-height:0;overflow:hidden;mso-hide:all">{Enc(preheader)}</div>""";

        var iconoHtml = string.IsNullOrEmpty(icono)
            ? string.Empty
            : $"""<div style="font-size:38px;text-align:center;margin:0 0 10px;line-height:1">{icono}</div>""";

        var ctaHtml = string.IsNullOrEmpty(enlaceCta)
            ? string.Empty
            : $"""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:28px auto 6px;border-collapse:collapse">
                  <tr>
                      <td style="background:#2563eb;border-radius:10px;mso-border-radius:10px">
                          <a href="{Enc(enlaceCta)}" style="display:inline-block;padding:13px 32px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;font-family:{fuente}">{Enc(textoCta)}</a>
                      </td>
                  </tr>
              </table>
              """;

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="x-apple-disable-message-reformatting">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            <title>{Enc(titulo)}</title>
            </head>
            <body style="margin:0;padding:0;background:#eef2f7">
                {preheaderHtml}
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="width:100%;background:#eef2f7;border-collapse:collapse">
                    <tr>
                        <td align="center" style="padding:24px 12px">
                            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:600px;max-width:100%;margin:0 auto;border-collapse:collapse">
                                <tr>
                                    <td style="background:#123B8F;padding:30px 24px;text-align:center;border-radius:16px 16px 0 0">
                                        <div style="font-family:{fuente};font-size:24px;font-weight:700;color:#ffffff;letter-spacing:.5px">RemesaSmart SV</div>
                                        <div style="font-family:{fuente};font-size:11px;color:#a9c1f0;text-transform:uppercase;letter-spacing:2px;margin-top:8px">Finanzas familiares y remesas</div>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background:#ffffff;padding:34px 30px;border-radius:0 0 16px 16px;box-shadow:0 4px 14px rgba(18,59,143,.12);font-family:{fuente}">
                                        {iconoHtml}
                                        <h2 style="color:#111827;margin:0 0 18px;font-size:21px;text-align:center;font-weight:700">{Enc(titulo)}</h2>
                                        {cuerpoHtml}
                                        {ctaHtml}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:22px 8px 4px;text-align:center;font-family:{fuente};color:#94a3b8;font-size:11px;line-height:1.7">
                                        <p style="margin:3px 0">© {anio} RemesaSmart SV · Mensaje automático, no respondas a este correo.</p>
                                        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="width:100%;border-collapse:collapse;margin:12px 0">
                                            <tr>
                                                <td style="background:#f8fafc;border-left:4px solid #2563eb;padding:12px 16px;text-align:left;color:#4b5563;font-size:12px;line-height:1.6">
                                                    Por tu seguridad, RemesaSmart nunca te pedirá tu contraseña, códigos de verificación ni datos bancarios por correo.
                                                </td>
                                            </tr>
                                        </table>
                                        <p style="margin:3px 0">Si no esperabas este correo, puedes ignorarlo sin problema.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
    {
        var (asunto, html, texto) = ConstruirInvitacionFamiliar(nombreFamilia, nombreInvitador, token);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirInvitacionFamiliar(string nombreFamilia, string nombreInvitador, string token)
    {
        var enlace = $"{BaseUrl()}/login?reset={token}";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{Enc(nombreInvitador)}</strong> te invitó a unirte a la familia
                <strong>{Enc(nombreFamilia)}</strong> en RemesaSmart SV.
            </p>
            <p style="color:#6b7280;font-size:13px">El enlace expira en 24 horas.</p>
            """;

        var html = Plantilla("Te invitaron a una familia", cuerpo, enlace, "Revisar invitación",
            icono: "👨‍👩‍👧", preheader: $"{nombreInvitador} te invitó a unirte a la familia {nombreFamilia}");

        var texto = $"RemesaSmart SV - Te invitaron a la familia {nombreFamilia}. Acepta en: {enlace}";

        return ($"Invitación a la familia {nombreFamilia}", html, texto);
    }

    public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
    {
        var (asunto, html, texto) = ConstruirResetContrasena(token);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirResetContrasena(string token)
    {
        var enlace = $"{BaseUrl()}/login?reset={token}";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                Recibimos una solicitud para restablecer tu contraseña.
                Haz clic en el botón para continuar.
            </p>
            <p style="color:#6b7280;font-size:13px">
                El enlace expira en 1 hora y solo puede usarse una vez.
            </p>
            <p style="color:#4b5563;line-height:1.6">
                Si no reconoces esta actividad, cambia tu contraseña de inmediato.
            </p>
            """;

        var html = Plantilla("Restablecer contraseña", cuerpo, enlace, "Restablecer contraseña",
            icono: "🔑", preheader: "Restablece tu contraseña en un solo paso");

        var texto = $"RemesaSmart SV - Restablece tu contraseña en: {enlace}";

        return ("Restablecer contraseña - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
    {
        var (asunto, html, texto) = ConstruirAlertaPresupuesto(nombreUsuario, nombrePresupuesto, montoLimite, montoGastado, porcentaje);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirAlertaPresupuesto(string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
    {
        var enlace = $"{BaseUrl()}/presupuestos";
        var porcentajeTexto = porcentaje.ToString(CultureInfo.InvariantCulture);
        var anchoBarra = Enc(Math.Min(porcentaje, 100).ToString("F0", CultureInfo.InvariantCulture));

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{Enc(nombreUsuario)}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                Tu presupuesto <strong>{Enc(nombrePresupuesto)}</strong> ha alcanzado el <strong>{Enc(porcentajeTexto)}%</strong> de su límite.
            </p>
            <div style="background:#f9fafb;padding:20px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Límite:</strong> ${Enc(Monto(montoLimite))}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Gastado:</strong> ${Enc(Monto(montoGastado))}</p>
                <div style="background:#e5e7eb;border-radius:4px;height:8px;margin-top:10px">
                    <div style="background:{(porcentaje >= 100 ? "#dc2626" : "#f59e0b")};border-radius:4px;height:8px;width:{anchoBarra}%"></div>
                </div>
            </div>
            """;

        var html = Plantilla("Aviso de presupuesto", cuerpo, enlace, "Ver presupuesto",
            icono: "📊", preheader: $"{nombrePresupuesto}: alcanzaste el {porcentajeTexto}% de tu límite");

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} alcanzó el {porcentaje}% de su límite (${montoGastado:F2} de ${montoLimite:F2}).";

        return ("Aviso de presupuesto familiar — RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
    {
        var (asunto, html, texto) = ConstruirPresupuestoExcedido(nombreUsuario, nombrePresupuesto, montoLimite, montoGastado);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirPresupuestoExcedido(string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
    {
        var enlace = $"{BaseUrl()}/presupuestos";
        var diferencia = montoGastado - montoLimite;

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{Enc(nombreUsuario)}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                Tu presupuesto <strong>{Enc(nombrePresupuesto)}</strong> ha superado su límite establecido.
            </p>
            <div style="background:#fef2f2;padding:20px;border-radius:8px;margin:20px 0;border-left:4px solid #dc2626">
                <p style="margin:5px 0;color:#4b5563"><strong>Límite:</strong> ${Enc(Monto(montoLimite))}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Total registrado:</strong> ${Enc(Monto(montoGastado))}</p>
                <p style="margin:5px 0;color:#dc2626;font-weight:bold"><strong>Exceso:</strong> ${Enc(Monto(diferencia))}</p>
            </div>
            """;

        var html = Plantilla("Presupuesto excedido", cuerpo, enlace, "Ver detalles",
            icono: "🚨", preheader: $"{nombrePresupuesto} superó su límite por ${Monto(diferencia)}");

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} excedió el límite (${montoGastado:F2} de ${montoLimite:F2}, exceso ${diferencia:F2}).";

        return ("Tu presupuesto ha superado el límite establecido", html, texto);
    }

    public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
    {
        var (asuntoFinal, html, texto) = ConstruirNotificacionCuenta(asunto, mensaje);
        return EnviarAsync(correoDestino, asuntoFinal, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirNotificacionCuenta(string asunto, string mensaje)
    {
        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">{Enc(mensaje)}</p>
            """;

        var html = Plantilla(asunto, cuerpo);

        var texto = $"RemesaSmart SV - {asunto}: {mensaje}";

        return (asunto, html, texto);
    }

    public Task<bool> EnviarAvisoAccesoAsync(string correoDestino, DateTime fechaUtc, string? origen)
    {
        var (asunto, html, texto) = ConstruirAvisoAcceso(fechaUtc, origen);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirAvisoAcceso(DateTime fechaUtc, string? origen)
    {
        var fecha = $"{Fecha(fechaUtc)} UTC";
        var origenLinea = string.IsNullOrWhiteSpace(origen)
            ? """<p style="margin:5px 0;color:#4b5563"><strong>Origen:</strong> no disponible</p>"""
            : $"""<p style="margin:5px 0;color:#4b5563"><strong>Origen:</strong> {Enc(origen)}</p>""";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                Detectamos un <strong>nuevo acceso</strong> a tu cuenta de RemesaSmart SV.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha y hora:</strong> {Enc(fecha)}</p>
                {origenLinea}
            </div>
            <p style="color:#4b5563;line-height:1.6">Si este acceso lo hiciste tú, ignora este correo.</p>
            <p style="color:#4b5563;line-height:1.6">Si no reconoces esta actividad, cambia tu contraseña de inmediato.</p>
            """;

        var html = Plantilla("Nuevo acceso a tu cuenta", cuerpo,
            icono: "🔐", preheader: $"Nuevo acceso el {fecha}");

        var texto = $"RemesaSmart SV - Nuevo acceso a tu cuenta el {fecha}.";

        return ("Nuevo acceso a tu cuenta", html, texto);
    }

    public Task<bool> EnviarAvisoCambioContrasenaAsync(string correoDestino, DateTime fechaUtc)
    {
        var (asunto, html, texto) = ConstruirAvisoCambioContrasena(fechaUtc);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirAvisoCambioContrasena(DateTime fechaUtc)
    {
        var fecha = $"{Fecha(fechaUtc)} UTC";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                La contraseña de tu cuenta de RemesaSmart SV fue cambiada correctamente.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha y hora:</strong> {Enc(fecha)}</p>
            </div>
            <p style="color:#4b5563;line-height:1.6">
                Si no fuiste tú, restablece tu contraseña de inmediato desde la aplicación.
            </p>
            <p style="color:#4b5563;line-height:1.6">
                Si no reconoces esta actividad, cambia tu contraseña de inmediato.
            </p>
            """;

        var html = Plantilla("Tu contraseña fue cambiada", cuerpo,
            icono: "🛡️", preheader: $"Tu contraseña fue cambiada el {fecha}");

        var texto = $"RemesaSmart SV - Tu contraseña fue cambiada el {fecha}.";

        return ("Tu contraseña fue cambiada", html, texto);
    }

    public Task<bool> EnviarResultadoSolicitudUnionAsync(string correoDestino, string nombreSolicitante, string nombreFamilia, bool aceptada)
    {
        var (asunto, html, texto) = ConstruirResultadoSolicitudUnion(nombreSolicitante, nombreFamilia, aceptada);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirResultadoSolicitudUnion(string nombreSolicitante, string nombreFamilia, bool aceptada)
    {
        var titulo = aceptada ? "Solicitud de unión aceptada" : "Solicitud de unión rechazada";

        var cuerpo = aceptada
            ? $"""
              <p style="color:#4b5563;line-height:1.6">Hola <strong>{Enc(nombreSolicitante)}</strong>,</p>
              <p style="color:#4b5563;line-height:1.6">
                  Tu solicitud para unirte a la familia <strong>{Enc(nombreFamilia)}</strong> fue
                  <strong>aceptada</strong>. Inicia sesión para acceder a ella.
              </p>
              """
            : $"""
              <p style="color:#4b5563;line-height:1.6">Hola <strong>{Enc(nombreSolicitante)}</strong>,</p>
              <p style="color:#4b5563;line-height:1.6">
                  Tu solicitud para unirte a la familia <strong>{Enc(nombreFamilia)}</strong> fue
                  <strong>rechazada</strong>. Puedes explorar otras familias desde la aplicación.
              </p>
              """;

        var html = Plantilla(titulo, cuerpo, aceptada ? $"{BaseUrl()}/login" : null, "Iniciar sesión",
            icono: aceptada ? "✅" : "❌", preheader: titulo);

        var texto = $"RemesaSmart SV - {titulo}: familia {nombreFamilia}.";

        return ($"{titulo} - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarResultadoInvitacionAsync(string correoDestino, string nombreFamilia, string nombreInvitado, bool aceptada)
    {
        var (asunto, html, texto) = ConstruirResultadoInvitacion(nombreFamilia, nombreInvitado, aceptada);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirResultadoInvitacion(string nombreFamilia, string nombreInvitado, bool aceptada)
    {
        var titulo = aceptada ? "Invitación aceptada" : "Invitación rechazada";

        var cuerpo = aceptada
            ? $"""
              <p style="color:#4b5563;line-height:1.6">
                  <strong>{Enc(nombreInvitado)}</strong> aceptó tu invitación y ya forma parte de la
                  familia <strong>{Enc(nombreFamilia)}</strong>.
              </p>
              """
            : $"""
              <p style="color:#4b5563;line-height:1.6">
                  <strong>{Enc(nombreInvitado)}</strong> rechazó tu invitación a la familia
                  <strong>{Enc(nombreFamilia)}</strong>.
              </p>
              """;

        var html = Plantilla(titulo, cuerpo, aceptada ? $"{BaseUrl()}/login" : null, "Ver mi familia",
            icono: aceptada ? "✅" : "❌", preheader: titulo);

        var texto = $"RemesaSmart SV - {titulo}: {nombreInvitado} y la familia {nombreFamilia}.";

        return ($"{titulo} - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarMovimientoFamiliaAsync(string correoDestino, string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
    {
        var (asunto, html, texto) = ConstruirMovimientoFamilia(nombreDestino, nombreAutor, tipoMovimiento, monto, descripcion, fecha);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirMovimientoFamilia(string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
    {
        var enlace = $"{BaseUrl()}/movimientos";
        var fechaTexto = Fecha(fecha);
        var descripcionLinea = string.IsNullOrWhiteSpace(descripcion)
            ? string.Empty
            : $"""<p style="margin:5px 0;color:#4b5563"><strong>Descripción:</strong> {Enc(descripcion)}</p>""";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{Enc(nombreDestino)}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                <strong>{Enc(nombreAutor)}</strong> registró un <strong>{Enc(tipoMovimiento)}</strong> de
                <strong>${Enc(Monto(monto))}</strong> en la actividad de la familia.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha:</strong> {Enc(fechaTexto)}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Tipo:</strong> {Enc(tipoMovimiento)}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Monto:</strong> ${Enc(Monto(monto))}</p>
                {descripcionLinea}
            </div>
            """;

        var asunto = $"{nombreAutor} registró un {tipoMovimiento} de ${monto:F2}";

        var html = Plantilla("Nuevo movimiento en la familia", cuerpo, enlace, "Ver movimientos",
            icono: "💸", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return (asunto, html, texto);
    }

    public Task<bool> EnviarUnionFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
    {
        var (asunto, html, texto) = ConstruirUnionFamilia(nombreFamilia, nombrePersona);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirUnionFamilia(string nombreFamilia, string nombrePersona)
    {
        var enlace = $"{BaseUrl()}/login";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{Enc(nombrePersona)}</strong> se unió a la familia <strong>{Enc(nombreFamilia)}</strong>.
            </p>
            <p style="color:#6b7280;font-size:13px">
                Ya forma parte de la actividad compartida de la familia en RemesaSmart SV.
            </p>
            """;

        var asunto = $"{nombrePersona} se unió a la familia {nombreFamilia}";

        var html = Plantilla("Nuevo miembro en la familia", cuerpo, enlace, "Ver mi familia",
            icono: "🎉", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return (asunto, html, texto);
    }

    public Task<bool> EnviarSalidaFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
    {
        var (asunto, html, texto) = ConstruirSalidaFamilia(nombreFamilia, nombrePersona);
        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    internal static (string Asunto, string Html, string Texto) ConstruirSalidaFamilia(string nombreFamilia, string nombrePersona)
    {
        var enlace = $"{BaseUrl()}/login";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{Enc(nombrePersona)}</strong> salió de la familia <strong>{Enc(nombreFamilia)}</strong>.
            </p>
            <p style="color:#6b7280;font-size:13px">
                Los datos financieros de la familia se conservan sin cambios.
            </p>
            """;

        var asunto = $"{nombrePersona} salió de la familia {nombreFamilia}";

        var html = Plantilla("Un miembro salió de la familia", cuerpo, enlace, "Ver mi familia",
            icono: "👋", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return (asunto, html, texto);
    }
}

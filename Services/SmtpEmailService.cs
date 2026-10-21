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

    /// <summary>
    /// Plantilla base compartida por todos los correos. Diseño transactional:
    /// preheader oculto (texto de vista previa en la bandeja), header con marca y
    /// tagline, tipografía moderna, ícono por tipo de correo, tarjeta con sombra
    /// redondeada, CTA con elevación y footer con aviso. Elimina las copias casi
    /// idénticas de HTML que existían por cada tipo de notificación.
    /// </summary>
    private static string Plantilla(
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
            : $"""<div style="display:none;max-height:0;overflow:hidden;mso-hide:all">{preheader}</div>""";

        var iconoHtml = string.IsNullOrEmpty(icono)
            ? string.Empty
            : $"""<div style="font-size:38px;text-align:center;margin:0 0 10px;line-height:1">{icono}</div>""";

        var ctaHtml = string.IsNullOrEmpty(enlaceCta)
            ? string.Empty
            : $"""
              <div style="text-align:center;margin:28px 0 6px">
                  <a href="{enlaceCta}" style="background:#2563eb;color:#ffffff;padding:13px 32px;border-radius:10px;text-decoration:none;font-weight:600;display:inline-block;font-family:{fuente};box-shadow:0 3px 10px rgba(37,99,235,.35)">
                      {textoCta}
                  </a>
              </div>
              """;

        return $"""
            <html>
            <body style="margin:0;padding:0;background:#eef2f7;">
                {preheaderHtml}
                <div style="font-family:{fuente};max-width:600px;margin:0 auto;padding:24px 12px">
                    <div style="background:#123B8F;border-radius:16px 16px 0 0;padding:30px 24px;text-align:center">
                        <div style="font-size:24px;font-weight:700;color:#ffffff;letter-spacing:.5px">RemesaSmart SV</div>
                        <div style="font-size:11px;color:#a9c1f0;text-transform:uppercase;letter-spacing:2px;margin-top:8px">Finanzas familiares y remesas</div>
                    </div>
                    <div style="background:#ffffff;padding:34px 30px;border-radius:0 0 16px 16px;box-shadow:0 4px 14px rgba(18,59,143,.12)">
                        {iconoHtml}
                        <h2 style="color:#111827;margin:0 0 18px;font-size:21px;text-align:center;font-weight:700">{titulo}</h2>
                        {cuerpoHtml}
                        {ctaHtml}
                    </div>
                    <div style="text-align:center;padding:22px 8px 4px;color:#94a3b8;font-size:11px;line-height:1.7">
                        <p style="margin:3px 0">© {anio} RemesaSmart SV · Finanzas familiares y remesas de forma simple</p>
                        <p style="margin:3px 0">Si no esperabas este correo, puedes ignorarlo sin problema.</p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login?reset={token}";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{nombreInvitador}</strong> te invitó a unirte a la familia
                <strong>{nombreFamilia}</strong> en RemesaSmart SV.
            </p>
            <p style="color:#6b7280;font-size:13px">El enlace expira en 24 horas.</p>
            """;

        var html = Plantilla("Te invitaron a una familia", cuerpo, enlace, "Revisar invitación",
            icono: "👨‍👩‍👧", preheader: $"{nombreInvitador} te invitó a unirte a la familia {nombreFamilia}");

        var texto = $"RemesaSmart SV - Te invitaron a la familia {nombreFamilia}. Acepta en: {enlace}";

        return EnviarAsync(correoDestino, $"Invitación a la familia {nombreFamilia}", html, texto);
    }

    public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login?reset={token}";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                Recibimos una solicitud para restablecer tu contraseña.
                Haz clic en el botón para continuar.
            </p>
            <p style="color:#6b7280;font-size:13px">
                El enlace expira en 1 hora y solo puede usarse una vez.
            </p>
            """;

        var html = Plantilla("Restablecer contraseña", cuerpo, enlace, "Restablecer contraseña",
            icono: "🔑", preheader: "Restablece tu contraseña en un solo paso");

        var texto = $"RemesaSmart SV - Restablece tu contraseña en: {enlace}";

        return EnviarAsync(correoDestino, "Restablecer contraseña - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/presupuestos";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreUsuario}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                Tu presupuesto <strong>{nombrePresupuesto}</strong> ha alcanzado el <strong>{porcentaje}%</strong> de su límite.
            </p>
            <div style="background:#f9fafb;padding:20px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Límite:</strong> ${montoLimite:F2}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Gastado:</strong> ${montoGastado:F2}</p>
                <div style="background:#e5e7eb;border-radius:4px;height:8px;margin-top:10px">
                    <div style="background:{(porcentaje >= 100 ? "#dc2626" : "#f59e0b")};border-radius:4px;height:8px;width:{Math.Min(porcentaje, 100):F0}%"></div>
                </div>
            </div>
            """;

        var html = Plantilla("Aviso de presupuesto", cuerpo, enlace, "Ver presupuesto",
            icono: "📊", preheader: $"{nombrePresupuesto}: alcanzaste el {porcentaje}% de tu límite");

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} alcanzó el {porcentaje}% de su límite (${montoGastado:F2} de ${montoLimite:F2}).";

        return EnviarAsync(correoDestino, "Aviso de presupuesto familiar — RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/presupuestos";
        var diferencia = montoGastado - montoLimite;

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreUsuario}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                Tu presupuesto <strong>{nombrePresupuesto}</strong> ha superado su límite establecido.
            </p>
            <div style="background:#fef2f2;padding:20px;border-radius:8px;margin:20px 0;border-left:4px solid #dc2626">
                <p style="margin:5px 0;color:#4b5563"><strong>Límite:</strong> ${montoLimite:F2}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Total registrado:</strong> ${montoGastado:F2}</p>
                <p style="margin:5px 0;color:#dc2626;font-weight:bold"><strong>Exceso:</strong> ${diferencia:F2}</p>
            </div>
            """;

        var html = Plantilla("Presupuesto excedido", cuerpo, enlace, "Ver detalles",
            icono: "🚨", preheader: $"{nombrePresupuesto} superó su límite por ${diferencia:F2}");

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} excedió el límite (${montoGastado:F2} de ${montoLimite:F2}, exceso ${diferencia:F2}).";

        return EnviarAsync(correoDestino, "Tu presupuesto ha superado el límite establecido", html, texto);
    }

    public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
    {
        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">{mensaje}</p>
            """;

        var html = Plantilla(asunto, cuerpo);

        var texto = $"RemesaSmart SV - {asunto}: {mensaje}";

        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    public Task<bool> EnviarAvisoAccesoAsync(string correoDestino, DateTime fechaUtc, string? origen)
    {
        var fecha = $"{fechaUtc:dd/MM/yyyy HH:mm} UTC";
        var origenLinea = string.IsNullOrWhiteSpace(origen)
            ? """<p style="margin:5px 0;color:#4b5563"><strong>Origen:</strong> no disponible</p>"""
            : $"""<p style="margin:5px 0;color:#4b5563"><strong>Origen:</strong> {origen}</p>""";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                Detectamos un <strong>nuevo acceso</strong> a tu cuenta de RemesaSmart SV.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha y hora:</strong> {fecha}</p>
                {origenLinea}
            </div>
            <p style="color:#4b5563;line-height:1.6">Si este acceso lo hiciste tú, ignora este correo.</p>
            """;

        var html = Plantilla("Nuevo acceso a tu cuenta", cuerpo,
            icono: "🔐", preheader: $"Nuevo acceso el {fecha}");

        var texto = $"RemesaSmart SV - Nuevo acceso a tu cuenta el {fecha}.";

        return EnviarAsync(correoDestino, "Nuevo acceso a tu cuenta", html, texto);
    }

    public Task<bool> EnviarAvisoCambioContrasenaAsync(string correoDestino, DateTime fechaUtc)
    {
        var fecha = $"{fechaUtc:dd/MM/yyyy HH:mm} UTC";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                La contraseña de tu cuenta de RemesaSmart SV fue cambiada correctamente.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha y hora:</strong> {fecha}</p>
            </div>
            <p style="color:#4b5563;line-height:1.6">
                Si no fuiste tú, restablece tu contraseña de inmediato desde la aplicación.
            </p>
            """;

        var html = Plantilla("Tu contraseña fue cambiada", cuerpo,
            icono: "🛡️", preheader: $"Tu contraseña fue cambiada el {fecha}");

        var texto = $"RemesaSmart SV - Tu contraseña fue cambiada el {fecha}.";

        return EnviarAsync(correoDestino, "Tu contraseña fue cambiada", html, texto);
    }

    public Task<bool> EnviarResultadoSolicitudUnionAsync(string correoDestino, string nombreSolicitante, string nombreFamilia, bool aceptada)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var titulo = aceptada ? "Solicitud de unión aceptada" : "Solicitud de unión rechazada";

        var cuerpo = aceptada
            ? $"""
              <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreSolicitante}</strong>,</p>
              <p style="color:#4b5563;line-height:1.6">
                  Tu solicitud para unirte a la familia <strong>{nombreFamilia}</strong> fue
                  <strong>aceptada</strong>. Inicia sesión para acceder a ella.
              </p>
              """
            : $"""
              <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreSolicitante}</strong>,</p>
              <p style="color:#4b5563;line-height:1.6">
                  Tu solicitud para unirte a la familia <strong>{nombreFamilia}</strong> fue
                  <strong>rechazada</strong>. Puedes explorar otras familias desde la aplicación.
              </p>
              """;

        var html = Plantilla(titulo, cuerpo, aceptada ? $"{baseUrl}/login" : null, "Iniciar sesión",
            icono: aceptada ? "✅" : "❌", preheader: titulo);

        var texto = $"RemesaSmart SV - {titulo}: familia {nombreFamilia}.";

        return EnviarAsync(correoDestino, $"{titulo} - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarResultadoInvitacionAsync(string correoDestino, string nombreFamilia, string nombreInvitado, bool aceptada)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var titulo = aceptada ? "Invitación aceptada" : "Invitación rechazada";

        var cuerpo = aceptada
            ? $"""
              <p style="color:#4b5563;line-height:1.6">
                  <strong>{nombreInvitado}</strong> aceptó tu invitación y ya forma parte de la
                  familia <strong>{nombreFamilia}</strong>.
              </p>
              """
            : $"""
              <p style="color:#4b5563;line-height:1.6">
                  <strong>{nombreInvitado}</strong> rechazó tu invitación a la familia
                  <strong>{nombreFamilia}</strong>.
              </p>
              """;

        var html = Plantilla(titulo, cuerpo, aceptada ? $"{baseUrl}/login" : null, "Ver mi familia",
            icono: aceptada ? "✅" : "❌", preheader: titulo);

        var texto = $"RemesaSmart SV - {titulo}: {nombreInvitado} y la familia {nombreFamilia}.";

        return EnviarAsync(correoDestino, $"{titulo} - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarMovimientoFamiliaAsync(string correoDestino, string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/movimientos";
        var fechaTexto = $"{fecha:dd/MM/yyyy HH:mm}";
        var descripcionLinea = string.IsNullOrWhiteSpace(descripcion)
            ? string.Empty
            : $"""<p style="margin:5px 0;color:#4b5563"><strong>Descripción:</strong> {descripcion}</p>""";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreDestino}</strong>,</p>
            <p style="color:#4b5563;line-height:1.6">
                <strong>{nombreAutor}</strong> registró un <strong>{tipoMovimiento}</strong> de
                <strong>${monto:F2}</strong> en la actividad de la familia.
            </p>
            <div style="background:#f9fafb;padding:16px;border-radius:8px;margin:20px 0">
                <p style="margin:5px 0;color:#4b5563"><strong>Fecha:</strong> {fechaTexto}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Tipo:</strong> {tipoMovimiento}</p>
                <p style="margin:5px 0;color:#4b5563"><strong>Monto:</strong> ${monto:F2}</p>
                {descripcionLinea}
            </div>
            """;

        var asunto = $"{nombreAutor} registró un {tipoMovimiento} de ${monto:F2}";

        var html = Plantilla("Nuevo movimiento en la familia", cuerpo, enlace, "Ver movimientos",
            icono: "💸", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    public Task<bool> EnviarUnionFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{nombrePersona}</strong> se unió a la familia <strong>{nombreFamilia}</strong>.
            </p>
            <p style="color:#6b7280;font-size:13px">
                Ya forma parte de la actividad compartida de la familia en RemesaSmart SV.
            </p>
            """;

        var asunto = $"{nombrePersona} se unió a la familia {nombreFamilia}";

        var html = Plantilla("Nuevo miembro en la familia", cuerpo, enlace, "Ver mi familia",
            icono: "🎉", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return EnviarAsync(correoDestino, asunto, html, texto);
    }

    public Task<bool> EnviarSalidaFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login";

        var cuerpo = $"""
            <p style="color:#4b5563;line-height:1.6">
                <strong>{nombrePersona}</strong> salió de la familia <strong>{nombreFamilia}</strong>.
            </p>
            <p style="color:#6b7280;font-size:13px">
                Los datos financieros de la familia se conservan sin cambios.
            </p>
            """;

        var asunto = $"{nombrePersona} salió de la familia {nombreFamilia}";

        var html = Plantilla("Un miembro salió de la familia", cuerpo, enlace, "Ver mi familia",
            icono: "👋", preheader: asunto);

        var texto = $"RemesaSmart SV - {asunto}.";

        return EnviarAsync(correoDestino, asunto, html, texto);
    }
}

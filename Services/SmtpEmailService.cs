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
            // No registrar destinatario, asunto, cuerpo ni excepción SMTP: pueden
            // contener PII, enlaces con tokens o detalles del proveedor.
            _logger.LogError("Error al enviar correo. Tipo de excepción: {ExceptionType}", ex.GetType().Name);
            return false;
        }
    }

    public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login?reset={token}";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#f3f4f6">
                <div style="background:#123B8F;padding:20px;border-radius:10px 10px 0 0;text-align:center">
                    <h1 style="color:white;margin:0">RemesaSmart SV</h1>
                </div>
                <div style="background:white;padding:30px;border-radius:0 0 10px 10px">
                    <h2 style="color:#1f2937">Te invitaron a una familia</h2>
                    <p style="color:#4b5563;line-height:1.6">
                        <strong>{nombreInvitador}</strong> te invitó a unirte a la familia
                        <strong>{nombreFamilia}</strong> en RemesaSmart SV.
                    </p>
                    <div style="text-align:center;margin:30px 0">
                        <a href="{enlace}" style="background:#2563eb;color:white;padding:12px 30px;border-radius:8px;text-decoration:none;font-weight:bold">
                            Revisar invitación
                        </a>
                    </div>
                    <p style="color:#6b7280;font-size:13px">
                        Si no esperabas este correo, puedes ignorarlo.
                        El enlace expira en 24 horas.
                    </p>
                </div>
            </div>
            """;

        var texto = $"RemesaSmart SV - Te invitaron a la familia {nombreFamilia}. Acepta en: {enlace}";

        return EnviarAsync(correoDestino, $"Invitación a la familia {nombreFamilia}", html, texto);
    }

    public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/login?reset={token}";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#f3f4f6">
                <div style="background:#123B8F;padding:20px;border-radius:10px 10px 0 0;text-align:center">
                    <h1 style="color:white;margin:0">RemesaSmart SV</h1>
                </div>
                <div style="background:white;padding:30px;border-radius:0 0 10px 10px">
                    <h2 style="color:#1f2937">Restablecer contraseña</h2>
                    <p style="color:#4b5563;line-height:1.6">
                        Recibimos una solicitud para restablecer tu contraseña.
                        Haz clic en el botón para continuar.
                    </p>
                    <div style="text-align:center;margin:30px 0">
                        <a href="{enlace}" style="background:#2563eb;color:white;padding:12px 30px;border-radius:8px;text-decoration:none;font-weight:bold">
                            Restablecer contraseña
                        </a>
                    </div>
                    <p style="color:#6b7280;font-size:13px">
                        El enlace expira en 1 hora y solo puede usarse una vez.
                        Si no solicitaste este cambio, ignora este correo.
                    </p>
                </div>
            </div>
            """;

        var texto = $"RemesaSmart SV - Restablece tu contraseña en: {enlace}";

        return EnviarAsync(correoDestino, "Restablecer contraseña - RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/presupuestos";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#f3f4f6">
                <div style="background:#123B8F;padding:20px;border-radius:10px 10px 0 0;text-align:center">
                    <h1 style="color:white;margin:0">RemesaSmart SV</h1>
                </div>
                <div style="background:white;padding:30px;border-radius:0 0 10px 10px">
                    <h2 style="color:#1f2937">Aviso de presupuesto</h2>
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
                    <div style="text-align:center;margin:30px 0">
                        <a href="{enlace}" style="background:#2563eb;color:white;padding:12px 30px;border-radius:8px;text-decoration:none;font-weight:bold">
                            Ver presupuesto
                        </a>
                    </div>
                </div>
            </div>
            """;

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} alcanzó el {porcentaje}% de su límite (${montoGastado:F2} de ${montoLimite:F2}).";

        return EnviarAsync(correoDestino, "Aviso de presupuesto familiar — RemesaSmart SV", html, texto);
    }

    public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
    {
        var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? "http://localhost:5173";
        var enlace = $"{baseUrl}/presupuestos";
        var diferencia = montoGastado - montoLimite;

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#f3f4f6">
                <div style="background:#dc2626;padding:20px;border-radius:10px 10px 0 0;text-align:center">
                    <h1 style="color:white;margin:0">⚠️ Presupuesto excedido</h1>
                </div>
                <div style="background:white;padding:30px;border-radius:0 0 10px 10px">
                    <p style="color:#4b5563;line-height:1.6">Hola <strong>{nombreUsuario}</strong>,</p>
                    <p style="color:#4b5563;line-height:1.6">
                        Tu presupuesto <strong>{nombrePresupuesto}</strong> ha superado su límite establecido.
                    </p>
                    <div style="background:#fef2f2;padding:20px;border-radius:8px;margin:20px 0;border-left:4px solid #dc2626">
                        <p style="margin:5px 0;color:#4b5563"><strong>Límite:</strong> ${montoLimite:F2}</p>
                        <p style="margin:5px 0;color:#4b5563"><strong>Total registrado:</strong> ${montoGastado:F2}</p>
                        <p style="margin:5px 0;color:#dc2626;font-weight:bold"><strong>Exceso:</strong> ${diferencia:F2}</p>
                    </div>
                    <div style="text-align:center;margin:30px 0">
                        <a href="{enlace}" style="background:#dc2626;color:white;padding:12px 30px;border-radius:8px;text-decoration:none;font-weight:bold">
                            Ver detalles
                        </a>
                    </div>
                </div>
            </div>
            """;

        var texto = $"RemesaSmart SV - Tu presupuesto {nombrePresupuesto} excedió el límite (${montoGastado:F2} de ${montoLimite:F2}, exceso ${diferencia:F2}).";

        return EnviarAsync(correoDestino, "Tu presupuesto ha superado el límite establecido", html, texto);
    }

    public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
    {
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#f3f4f6">
                <div style="background:#123B8F;padding:20px;border-radius:10px 10px 0 0;text-align:center">
                    <h1 style="color:white;margin:0">RemesaSmart SV</h1>
                </div>
                <div style="background:white;padding:30px;border-radius:0 0 10px 10px">
                    <p style="color:#4b5563;line-height:1.6">{mensaje}</p>
                    <p style="color:#6b7280;font-size:13px">Si no reconoces esta actividad, revisa la seguridad de tu cuenta.</p>
                </div>
            </div>
            """;

        var texto = $"RemesaSmart SV - {asunto}: {mensaje}";

        return EnviarAsync(correoDestino, asunto, html, texto);
    }
}

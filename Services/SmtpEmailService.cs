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
            _logger.LogWarning("SMTP no configurado (SMTP_USER/SMTP_PASS). Correo NO enviado a {Destino}.", destino);
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
            _logger.LogInformation("Correo enviado a {Destino}: {Asunto}", destino, asunto);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo a {Destino}", destino);
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
}

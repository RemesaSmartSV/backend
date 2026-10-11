namespace RemesaSmartSV.Services;

/// <summary>
/// Servicio de envío de correos electrónicos.
/// Implementación actual: SMTP con Gmail (remesasmartsv@gmail.com).
/// Upgrade path: Gmail API con OAuth 2.0 (ver docs/GMAIL_API.md).
/// </summary>
public interface IEmailService
{
    Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token);
    Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token);
}

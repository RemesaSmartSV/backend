using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Filters;

/// <summary>
/// Revalida en cada request autenticado que la membresía del usuario siga vigente
/// en la base de datos. El token JWT dura 8 h y sus claims (idHogar, rol) quedan
/// congelados al momento de firmarse: sin esta revalidación, un usuario expulsado
/// o que abandonó su familia conservaría acceso con el token viejo hasta que expire.
/// </summary>
public class TokenVigenciaFilter : IAsyncActionFilter
{
    private readonly ApplicationDbContext _db;

    public TokenVigenciaFilter(ApplicationDbContext db) => _db = db;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Endpoints anónimos (login, registro, reset, Google): no hay membresía
        // que validar, se continúa sin tocar nada.
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Defensa doble: aunque llegue un token adjunto a un endpoint anónimo
        // (p. ej. login), no se debe rechazar por membresía no vigente.
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
        {
            await next();
            return;
        }

        // Mismo patrón que ClaimsPrincipalExtensions: int.TryParse y 0 como
        // centinela de "claim ausente o inválido" → se rechaza.
        var idUsuario = context.HttpContext.User.GetIdUsuario();
        if (idUsuario == 0)
        {
            await ResponderNoVigente(context);
            return;
        }

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        // El usuario fue eliminado de la BD: el token ya no representa a nadie.
        if (usuario is null)
        {
            await ResponderNoVigente(context);
            return;
        }

        // El claim idHogar quedó congelado al firmar el token. Si en la BD el
        // usuario ya no tiene hogar (abandonó o fue expulsado) o cambió de
        // familia, el token ya no refleja la membresía vigente.
        var idHogarToken = context.HttpContext.User.GetIdHogar();
        if ((usuario.IdHogar ?? 0) != idHogarToken)
        {
            await ResponderNoVigente(context);
            return;
        }

        // El rol también puede cambiar (degradación/promoción) mientras el token
        // sigue vivo: se compara contra la BD para no perpetuar permisos viejos.
        var rolToken = context.HttpContext.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if ((usuario.Rol ?? string.Empty) != rolToken)
        {
            await ResponderNoVigente(context);
            return;
        }

        await next();
    }

    private static async Task ResponderNoVigente(ActionExecutingContext context)
    {
        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(new
            {
                message = "Tu sesión ya no corresponde a una membresía vigente. Inicia sesión nuevamente."
            })
        };
        await Task.CompletedTask;
    }
}

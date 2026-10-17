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
/// congelados al momento de firmarse.
///
/// Regla central: el token nunca puede afirmar más que la realidad. Si el token
/// afirma una membresía mayor a la vigente (una familia que ya no es la suya, o
/// el rol Admin sin respaldo en la BD), se responde 401. Si la subestima (recién
/// unido a una familia, o recién promovido a Admin), se deja pasar: el token no
/// otorga acceso y el usuario puede recuperarse con POST /Auth/refrescar-sesion.
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

        // El claim idHogar quedó congelado al firmar el token, y la comparación
        // con la BD es asimétrica: solo se rechaza si el token afirma una familia
        // que ya no es la vigente (expulsión, abandono o cambio de familia).
        // Si el token afirma idHogar=0 (sin familia) y la BD sí le asigna una
        // (recién unido), se deja pasar: el token subestima y, con GetIdHogar()
        // en 0, los endpoints del hogar devuelven vacío/404/400 o 403 — no hay
        // acceso a datos ajenos.
        var idHogarToken = context.HttpContext.User.GetIdHogar();
        if (idHogarToken != 0 && idHogarToken != (usuario.IdHogar ?? 0))
        {
            await ResponderNoVigente(context);
            return;
        }

        // El rol también puede cambiar mientras el token sigue vivo, y la
        // comparación vuelve a ser asimétrica: solo se rechaza si el token afirma
        // el privilegio más sensible (Admin) sin respaldo en la BD (degradación).
        // Cualquier otra diferencia —p. ej. token Miembro y BD Admin tras una
        // promoción— se deja pasar: el token subestima y [Authorize(Roles="Admin")]
        // deniega con 403 por la vía normal, dejando libre el flujo de
        // refrescar-sesion.
        var rolToken = context.HttpContext.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (rolToken == "Admin" && (usuario.Rol ?? string.Empty) != "Admin")
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

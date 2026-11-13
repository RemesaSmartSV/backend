using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
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
    private readonly IMemoryCache? _cache;

    // Ventana de caché de la validación de membresía. Reduce la consulta a BD por
    // request; a cambio, una expulsión/cambio de familia tarda hasta esta ventana en
    // reflejarse. Se mantiene corta para acotar esa demora de revocación.
    private const int VentanaCacheSegundos = 15;

    public TokenVigenciaFilter(ApplicationDbContext db, IMemoryCache? cache = null)
    {
        _db = db;
        _cache = cache;
    }

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

        var usuario = await ObtenerUsuarioAsync(idUsuario);

        // Camino rápido: si el usuario está en caché y el token no afirma una
        // membresía mayor que la vigente, se continúa sin consultar la BD.
        if (usuario is not null && !EsRechazo(context.HttpContext.User, usuario))
        {
            await next();
            return;
        }

        // Posible rechazo (o usuario no cacheado): se re-valida contra la BD antes
        // de responder 401 para no rechazar por una caché desactualizada (p. ej. el
        // usuario acaba de crear/unirse a una familia y la caché aún no lo refleja).
        usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario is not null)
            _cache?.Set($"TokenVigencia:{idUsuario}", usuario, TimeSpan.FromSeconds(VentanaCacheSegundos));
        if (usuario is null || EsRechazo(context.HttpContext.User, usuario))
        {
            await ResponderNoVigente(context);
            return;
        }

        await next();
    }

    /// <summary>Regla asimétrica de vigencia: el token nunca puede afirmar más que la
    /// realidad. Devuelve true si hay que rechazar por membresía no vigente.</summary>
    private static bool EsRechazo(ClaimsPrincipal user, Usuario usuario)
    {
        // Solo se rechaza si el token afirma una familia que ya no es la vigente
        // (expulsión, abandono o cambio de familia). Si el token afirma idHogar=0
        // (sin familia) y la BD sí le asigna una, se deja pasar: el token subestima.
        var idHogarToken = user.GetIdHogar();
        if (idHogarToken != 0 && idHogarToken != (usuario.IdHogar ?? 0))
            return true;

        // Solo se rechaza si el token afirma Admin sin respaldo en la BD (degradación).
        var rolToken = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (rolToken == "Admin" && (usuario.Rol ?? string.Empty) != "Admin")
            return true;

        return false;
    }

    /// <summary>Obtiene el usuario para validar la membresía, con caché en memoria de
    /// corta duración para no consultar la BD en cada request autenticado.</summary>
    private async Task<Usuario?> ObtenerUsuarioAsync(int idUsuario)
    {
        if (_cache is null)
            return await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        var clave = $"TokenVigencia:{idUsuario}";
        if (_cache.TryGetValue<Usuario>(clave, out var cacheado))
            return cacheado;

        var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario is not null)
            _cache.Set(clave, usuario, TimeSpan.FromSeconds(VentanaCacheSegundos));

        return usuario;
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

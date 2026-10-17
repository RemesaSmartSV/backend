using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Filters;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas unitarias de <see cref="TokenVigenciaFilter"/>: revalida la membresía
/// vigente en la BD. La regla es asimétrica —el token no puede afirmar más que
/// la realidad—: expulsión, cambio de familia o degradación de Admin dan 401,
/// pero un token que subestima la membresía (recién unido o recién promovido)
/// se deja pasar para no romper POST /Auth/refrescar-sesion.
/// </summary>
public class TokenVigenciaFilterTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public TokenVigenciaFilterTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Construye un ActionExecutingContext a mano con un ClaimsPrincipal armado,
    /// siguiendo el patrón de FamiliaAbandonoTests pero sin pasar por un controller.
    /// </summary>
    private ActionExecutingContext CrearContexto(IEnumerable<Claim> claims, bool autenticado = true, bool allowAnonymous = false)
    {
        var identity = autenticado
            ? new ClaimsIdentity(claims, "Test")
            : new ClaimsIdentity(); // sin tipo de autenticación → IsAuthenticated false

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var actionDescriptor = new ControllerActionDescriptor();
        if (allowAnonymous)
            // EndpointMetadata viene como Array.Empty<object> (tamaño fijo):
            // hay que asignar una lista nueva, no hacer .Add sobre el array.
            actionDescriptor.EndpointMetadata = new List<object> { new AllowAnonymousAttribute() };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private async Task<(bool nextLlamado, ActionExecutingContext contexto)> EjecutarFiltroAsync(
        IEnumerable<Claim> claims, bool autenticado = true, bool allowAnonymous = false)
    {
        var contexto = CrearContexto(claims, autenticado, allowAnonymous);
        var filtro = new TokenVigenciaFilter(_db);

        var nextLlamado = false;
        await filtro.OnActionExecutionAsync(contexto, () =>
        {
            nextLlamado = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        return (nextLlamado, contexto);
    }

    private static List<Claim> ClaimsDe(int idUsuario, int idHogar, string rol)
        => new()
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString()),
            new(ClaimTypes.Role, rol)
        };

    private async Task<Usuario> SembrarUsuarioAsync(int idUsuario, int? idHogar, string? rol)
    {
        var usuario = new Usuario
        {
            IdUsuario = idUsuario,
            IdHogar = idHogar,
            Nombre = "Usuario",
            Correo = $"usuario{idUsuario}@gmail.com",
            ContrasenaHash = "x",
            Rol = rol,
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task ClaimsCoincidenConBD_PasaYSigueNext()
    {
        await SembrarUsuarioAsync(1, 5, "Miembro");

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 5, "Miembro"));

        Assert.True(nextLlamado);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task UsuarioExpulsado_BDIdHogarNullClaimConHogar_Retorna401()
    {
        // Expulsado / abandonó: en la BD ya no tiene hogar, pero el token viejo
        // aún porta el claim idHogar=5.
        await SembrarUsuarioAsync(1, null, null);

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 5, "Miembro"));

        Assert.False(nextLlamado);
        var contenido = Assert.IsType<ContentResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, contenido.StatusCode);
        Assert.Equal("application/json", contenido.ContentType);
        // Se deserializa en vez de usar Assert.Contains sobre el string crudo:
        // System.Text.Json escapa los acentos (ó → \u00F3) por defecto.
        using var doc = JsonDocument.Parse(contenido.Content!);
        Assert.Equal(
            "Tu sesión ya no corresponde a una membresía vigente. Inicia sesión nuevamente.",
            doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AdminDegradoAMiembro_Retorna401()
    {
        // En la BD el usuario fue degradado a Miembro, pero el token aún dice
        // Admin: el token afirma el privilegio más sensible sin respaldo → 401.
        await SembrarUsuarioAsync(1, 5, "Miembro");

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 5, "Admin"));

        Assert.False(nextLlamado);
        var contenido = Assert.IsType<ContentResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, contenido.StatusCode);
    }

    [Fact]
    public async Task RecienUnido_BDTieneHogarClaimIdHogarCero_Pasa()
    {
        // Recién unido a una familia: la BD ya lo registró (IdHogar=5), pero el
        // token viejo aún afirma idHogar=0. El token subestima → se deja pasar
        // para que POST /Auth/refrescar-sesion pueda resincronizar la sesión.
        await SembrarUsuarioAsync(1, 5, "Miembro");

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 0, "Miembro"));

        Assert.True(nextLlamado);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task PromovidoAAdmin_BDRolAdminClaimMiembro_Pasa()
    {
        // Recién promovido: la BD ya dice Admin, pero el token viejo aún dice
        // Miembro. El token subestima → se deja pasar; si el endpoint exige
        // Admin, [Authorize] deniega con 403 por la vía normal.
        await SembrarUsuarioAsync(1, 5, "Admin");

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 5, "Miembro"));

        Assert.True(nextLlamado);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task CambioDeFamilia_ClaimIdHogar1_BDIdHogar2_Retorna401()
    {
        // Cambió de familia: el token aún afirma idHogar=1, pero la membresía
        // vigente es idHogar=2. El token afirma más que la realidad → 401.
        await SembrarUsuarioAsync(1, 2, "Miembro");

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(1, 1, "Miembro"));

        Assert.False(nextLlamado);
        var contenido = Assert.IsType<ContentResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, contenido.StatusCode);
    }

    [Fact]
    public async Task UsuarioInexistenteEnBD_Retorna401()
    {
        // BD vacía: el usuario fue eliminado, pero el token sigue siendo válido criptográficamente.
        var (nextLlamado, contexto) = await EjecutarFiltroAsync(ClaimsDe(999, 5, "Miembro"));

        Assert.False(nextLlamado);
        var contenido = Assert.IsType<ContentResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, contenido.StatusCode);
    }

    [Fact]
    public async Task NoAutenticado_PasaSinValidarMembresia()
    {
        // Identity.IsAuthenticated false → el filtro ni siquiera consulta la BD;
        // un endpoint anónimo (login, registro, reset) no debe ser bloqueado.
        var (nextLlamado, contexto) = await EjecutarFiltroAsync(
            Enumerable.Empty<Claim>(), autenticado: false);

        Assert.True(nextLlamado);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task EndpointAllowAnonymous_ConTokenViejo_PasaSinBloquear()
    {
        // Defensa doble: aunque llegue un token adjunto a un endpoint anónimo
        // (p. ej. login con una membresía ya no vigente), no se debe rechazar.
        await SembrarUsuarioAsync(1, null, null); // usuario sin hogar en BD

        var (nextLlamado, contexto) = await EjecutarFiltroAsync(
            ClaimsDe(1, 5, "Miembro"), allowAnonymous: true);

        Assert.True(nextLlamado);
        Assert.Null(contexto.Result);
    }
}

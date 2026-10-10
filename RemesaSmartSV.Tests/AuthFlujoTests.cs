using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas del flujo registro sin hogar -> creacion del hogar -> auto-servicio de perfil.
/// Cubre AuthService, HogaresController (POST) y UsuariosController (PUT perfil).
/// </summary>
public class AuthFlujoTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthFlujoTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

        // Jwt:Key de 64 bytes: HMAC-SHA256 exige como minimo 256 bits para firmar.
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "clave-de-prueba-remesasmart-0123456789-abcdefghij-0123456789",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            })
            .Build();
    }

    public void Dispose() => _db.Dispose();

    private AuthService CrearAuthService() => new(_db, _config);

    private static void AsignarClaims(ControllerBase controller, int idUsuario, int idHogarClaim)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogarClaim.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<Usuario> CrearUsuarioSinHogarAsync(string correo = "ana@example.com")
    {
        var usuario = new Usuario
        {
            Nombre = "Ana",
            Correo = correo,
            ContrasenaHash = "hash-de-prueba",
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task RegisterAsync_CreaUsuarioSinHogarYSinRol()
    {
        var auth = CrearAuthService();

        var response = await auth.RegisterAsync(
            new RegisterRequest { Nombre = "Ana Rivera", Correo = "ana@example.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

        Assert.NotNull(response);
        var usuario = await _db.Usuarios.SingleAsync();
        Assert.Null(usuario.IdHogar);
        Assert.Null(usuario.Rol);
        // El registro ya no debe crear hogares ni sembras categorias.
        Assert.False(await _db.Hogares.AnyAsync());
        Assert.False(await _db.Categorias.AnyAsync());
        // La ausencia de hogar se representa con null, no con centinelas 0/"".
        Assert.Null(response!.IdHogar);
        Assert.Null(response.Rol);
        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    [Fact]
    public async Task RegisterAsync_CorreoDuplicado_DevuelveNull()
    {
        var auth = CrearAuthService();
        await auth.RegisterAsync(
            new RegisterRequest { Nombre = "Ana Rivera", Correo = "ana@example.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

        // El control de duplicados ignora mayusculas, como hace el servicio.
        var duplicado = await auth.RegisterAsync(
            new RegisterRequest { Nombre = "Otra Persona", Correo = "ANA@Example.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

        Assert.Null(duplicado);
        Assert.Equal(1, await _db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task HogaresController_Post_CreaHogarAsignaAdminYSiembraCategorias()
    {
        var auth = CrearAuthService();
        var usuario = await CrearUsuarioSinHogarAsync();
        var controller = new HogaresController(_db, auth);
        // Token de un usuario sin hogar: el claim idHogar llega en 0.
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var result = await controller.Create(new CrearHogarRequest("Familia Rivera"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var respuesta = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.False(string.IsNullOrEmpty(respuesta.Token));
        Assert.NotNull(respuesta.IdHogar);

        var hogar = Assert.IsType<Hogar>(await _db.Hogares.SingleAsync());
        Assert.Equal("Familia Rivera", hogar.NombreFamiliar);

        var usuarioActualizado = await _db.Usuarios.SingleAsync();
        Assert.Equal(hogar.IdHogar, usuarioActualizado.IdHogar);
        Assert.Equal("Admin", usuarioActualizado.Rol);
        Assert.Equal(respuesta.IdHogar, usuarioActualizado.IdHogar);

        // Cambio 7 ampliado (M4b): el hogar nace con las 23 categorías del sistema.
        Assert.Equal(23, await _db.Categorias.CountAsync(c => c.EsSistema && c.IdHogar == hogar.IdHogar));
    }

    [Fact]
    public async Task HogaresController_Post_SiYaTieneHogar_DevuelveConflict()
    {
        var hogarExistente = new Hogar { NombreFamiliar = "Familia Existente" };
        _db.Hogares.Add(hogarExistente);
        await _db.SaveChangesAsync();
        var usuario = new Usuario
        {
            IdHogar = hogarExistente.IdHogar,
            Nombre = "Ana",
            Correo = "ana@example.com",
            ContrasenaHash = "hash-de-prueba",
            Rol = "Admin"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new HogaresController(_db, CrearAuthService());
        AsignarClaims(controller, usuario.IdUsuario, hogarExistente.IdHogar);

        var result = await controller.Create(new CrearHogarRequest("Familia Duplicada"));

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("Ya pertenec", conflictResult.Value?.ToString());
        // No se creo un segundo hogar ni se sembro nada.
        Assert.Equal(1, await _db.Hogares.CountAsync());
        Assert.Equal(0, await _db.Categorias.CountAsync());
    }

    [Fact]
    public async Task UsuariosController_ActualizarPerfil_CambiaNombreYNiTocaRolNiHogar()
    {
        var hogar = new Hogar { NombreFamiliar = "Familia Rivera" };
        _db.Hogares.Add(hogar);
        await _db.SaveChangesAsync();
        var usuario = new Usuario
        {
            IdHogar = hogar.IdHogar,
            Nombre = "Ana",
            Correo = "ana@example.com",
            ContrasenaHash = "hash-de-prueba",
            Rol = "Miembro"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new UsuariosController(_db);
        AsignarClaims(controller, usuario.IdUsuario, hogar.IdHogar);

        var result = await controller.ActualizarPerfil(new ActualizarPerfilRequest("Ana Rivera"));

        Assert.IsType<NoContentResult>(result);
        var actualizado = await _db.Usuarios.SingleAsync();
        Assert.Equal("Ana Rivera", actualizado.Nombre);
        // El punto del endpoint: auto-servicio que no puede tocar rol ni hogar.
        Assert.Equal("Miembro", actualizado.Rol);
        Assert.Equal(hogar.IdHogar, actualizado.IdHogar);
    }

    [Fact]
    public void RegisterRequest_ConfirmarContrasenaDistinto_CompareDevuelveError()
    {
        var request = new RegisterRequest
        {
            Nombre = "Ana Rivera",
            Correo = "ana@example.com",
            Contrasena = "ClaveSegura123",
            ConfirmarContrasena = "ClaveQueNoCoincide"
        };

        var resultados = new List<ValidationResult>();
        var esValido = Validator.TryValidateObject(
            request, new ValidationContext(request), resultados, validateAllProperties: true);

        Assert.False(esValido);
        Assert.Contains(resultados,
            r => r.MemberNames.Contains(nameof(RegisterRequest.ConfirmarContrasena)));
    }

    [Fact]
    public void RegisterRequest_ConfirmarContrasenaIgual_PasaValidacion()
    {
        var request = new RegisterRequest
        {
            Nombre = "Ana Rivera",
            Correo = "ana@example.com",
            Contrasena = "ClaveSegura123",
            ConfirmarContrasena = "ClaveSegura123"
        };

        var resultados = new List<ValidationResult>();
        var esValido = Validator.TryValidateObject(
            request, new ValidationContext(request), resultados, validateAllProperties: true);

        Assert.True(esValido);
    }
}

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

    private async Task<Usuario> CrearUsuarioSinHogarAsync(string correo = "ana@gmail.com")
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

    /// <summary>
    /// Stub de IEmailService que registra cada envío como (destino, asunto) para
    /// assertar el cableado de notificaciones sin depender del SMTP real.
    /// </summary>
    private class EmailRegistrador : IEmailService
    {
        public List<(string Destino, string Asunto)> Enviados { get; } = new();

        public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
        { Enviados.Add((correoDestino, "invitacion-familiar")); return Task.FromResult(true); }
        public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
        { Enviados.Add((correoDestino, "reset-contrasena")); return Task.FromResult(true); }
        public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
        { Enviados.Add((correoDestino, "alerta-presupuesto")); return Task.FromResult(true); }
        public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
        { Enviados.Add((correoDestino, "presupuesto-excedido")); return Task.FromResult(true); }
        public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
        { Enviados.Add((correoDestino, "notificacion-cuenta")); return Task.FromResult(true); }
        public Task<bool> EnviarAvisoAccesoAsync(string correoDestino, DateTime fechaUtc, string? origen)
        { Enviados.Add((correoDestino, "aviso-acceso")); return Task.FromResult(true); }
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string correoDestino, DateTime fechaUtc)
        { Enviados.Add((correoDestino, "aviso-cambio-contrasena")); return Task.FromResult(true); }
    }

    [Fact]
    public async Task RegisterAsync_CreaUsuarioSinHogarYSinRol()
    {
        var auth = CrearAuthService();

        var response = await auth.RegisterAsync(
            new RegisterRequest { Nombre = "Ana Rivera", Correo = "ana@gmail.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

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
            new RegisterRequest { Nombre = "Ana Rivera", Correo = "ana@gmail.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

        // El control de duplicados ignora mayusculas, como hace el servicio.
        var duplicado = await auth.RegisterAsync(
            new RegisterRequest { Nombre = "Otra Persona", Correo = "ANA@Example.com", Contrasena = "ClaveSegura123", ConfirmarContrasena = "ClaveSegura123" });

        Assert.Null(duplicado);
        Assert.Equal(1, await _db.Usuarios.CountAsync());
    }

    /// <summary>
    /// Verifica la configuración del índice unique de Usuarios.Correo (no su
    /// comportamiento en BD): evita dos cuentas con el mismo correo, lo que
    /// dejaría el login ambiguo. EF InMemory construye el modelo pero no
    /// ejecuta los índices.
    /// </summary>
    [Fact]
    public void Modelo_IndiceDeCorreoDeUsuarioEsUnico()
    {
        var entidad = _db.Model.FindEntityType(typeof(Usuario));
        Assert.NotNull(entidad);

        var indice = entidad!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Correo" }));

        Assert.NotNull(indice);
        Assert.True(indice!.IsUnique);
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
            Correo = "ana@gmail.com",
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
    public async Task HogaresController_Post_TokenViejoConHogarYaAbandonado_CreaElHogar()
    {
        // Regresión: el claim idHogar del token se firma una vez y vive 8 h, así que
        // sigue diciendo "tenés hogar" aunque el usuario acabara de abandonar. El
        // controlador chequeaba ese claim ANTES de leer la BD y respondía 409
        // indebido. La autoridad sobre la membresía es la fila, no el token.
        var usuario = await CrearUsuarioSinHogarAsync();
        var controller = new HogaresController(_db, CrearAuthService());
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 99);

        var result = await controller.Create(new CrearHogarRequest("Familia Nueva"));

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var respuesta = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.False(string.IsNullOrEmpty(respuesta.Token));

        var hogar = Assert.IsType<Hogar>(await _db.Hogares.SingleAsync());
        var usuarioActualizado = await _db.Usuarios.SingleAsync();
        Assert.Equal(hogar.IdHogar, usuarioActualizado.IdHogar);
        Assert.Equal("Admin", usuarioActualizado.Rol);
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
            Correo = "ana@gmail.com",
            ContrasenaHash = "hash-de-prueba",
            Rol = "Miembro"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new UsuariosController(_db, new AuthService(_db, _config), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario, hogar.IdHogar);

        var result = await controller.ActualizarPerfil(new ActualizarPerfilRequest
        {
            Nombre = "Ana Rivera",
            IdiomaPreferido = "en"
        });

        Assert.IsType<NoContentResult>(result);
        var actualizado = await _db.Usuarios.SingleAsync();
        Assert.Equal("Ana Rivera", actualizado.Nombre);
        Assert.Equal("en", actualizado.IdiomaPreferido);
        // El punto del endpoint: auto-servicio que no puede tocar rol ni hogar.
        Assert.Equal("Miembro", actualizado.Rol);
        Assert.Equal(hogar.IdHogar, actualizado.IdHogar);
    }

    [Fact]
    public async Task BuildLoginResponse_IncluyeIdiomaPreferidoPersistido()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        usuario.IdiomaPreferido = "en";

        var respuesta = CrearAuthService().BuildLoginResponse(usuario);

        Assert.Equal("en", respuesta.IdiomaPreferido);
    }

    [Fact]
    public async Task UsuariosController_ActualizarIdioma_PersisteIdiomaSoportado()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        var controller = new UsuariosController(_db, CrearAuthService(), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var result = await controller.ActualizarIdioma(new ActualizarIdiomaRequest { IdiomaPreferido = "EN" });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("en", (await _db.Usuarios.SingleAsync()).IdiomaPreferido);
    }

    [Fact]
    public async Task UsuariosController_ActualizarIdioma_RechazaIdiomaNoSoportado()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        var controller = new UsuariosController(_db, CrearAuthService(), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var result = await controller.ActualizarIdioma(new ActualizarIdiomaRequest { IdiomaPreferido = "fr" });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await _db.Usuarios.SingleAsync()).IdiomaPreferido);
    }

    [Fact]
    public async Task UsuariosController_AddMember_DominioNoGmail_RechazaSinCrearCuenta()
    {
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            IdHogar = 7,
            Nombre = "Admin",
            Correo = "admin@gmail.com",
            ContrasenaHash = "hash",
            Rol = "Admin",
            FechaRegistro = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        var controller = new UsuariosController(_db, CrearAuthService(), new EmailRegistrador());
        AsignarClaims(controller, idUsuario: 1, idHogarClaim: 7);

        var resultado = await controller.AddMember(
            new AddMemberRequest("Legacy", "legacy@example.org", "ClaveSegura123", "Miembro"));

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Single(await _db.Usuarios.ToListAsync());
        Assert.False(await _db.Usuarios.AnyAsync(u => u.Correo == "legacy@example.org"));
    }

    [Fact]
    public void RegisterRequest_ConfirmarContrasenaDistinto_CompareDevuelveError()
    {
        var request = new RegisterRequest
        {
            Nombre = "Ana Rivera",
            Correo = "ana@gmail.com",
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
            Correo = "ana@gmail.com",
            Contrasena = "ClaveSegura123",
            ConfirmarContrasena = "ClaveSegura123"
        };

        var resultados = new List<ValidationResult>();
        var esValido = Validator.TryValidateObject(
            request, new ValidationContext(request), resultados, validateAllProperties: true);

        Assert.True(esValido);
    }

    [Fact]
    public async Task Delete_AdminActual_NoPuedeEliminarseASiMismo()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);

        var admin = new Usuario { Nombre = "Admin", Correo = "admin@gmail.com", ContrasenaHash = "x", IdHogar = 1, Rol = "Admin", FechaRegistro = DateTime.UtcNow };
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();

        var controller = new UsuariosController(db, new AuthService(db, _config), new EmailRegistrador());
        var claims = new List<System.Security.Claims.Claim> { new("idUsuario", admin.IdUsuario.ToString()), new("idHogar", "1"), new(System.Security.Claims.ClaimTypes.Role, "Admin") };
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "Test")) },
        };

        var result = await controller.Delete(admin.IdUsuario);

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Contains("propio usuario", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Update_DegradarUltimoAdmin_RetornaBadRequest()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);

        var admin = new Usuario { Nombre = "Admin", Correo = "admin@gmail.com", ContrasenaHash = "x", IdHogar = 1, Rol = "Admin", FechaRegistro = DateTime.UtcNow };
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();

        var controller = new UsuariosController(db, new AuthService(db, _config), new EmailRegistrador());
        var claims = new List<System.Security.Claims.Claim> { new("idUsuario", admin.IdUsuario.ToString()), new("idHogar", "1"), new(System.Security.Claims.ClaimTypes.Role, "Admin") };
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "Test")) },
        };

        var result = await controller.Update(admin.IdUsuario, new UpdateUsuarioRequest(null, "Miembro"));

        var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Contains("último administrador", badRequest.Value?.ToString());
    }

    [Theory]
    [InlineData("usuario@gmail.com", true)]
    [InlineData("USUARIO@GMAIL.COM", true)]
    [InlineData("usuario@gmail.com.attacker.test", false)]
    [InlineData("usuario@example.com", false)]
    [InlineData("", false)]
    public void EsCorreoGmail_AplicaDominioExacto(string correo, bool esperado)
        => Assert.Equal(esperado, AuthService.EsCorreoGmail(correo));

    [Fact]
    public async Task LoginAsync_CuentaLegacyNoGmail_NoPermiteAutenticacion()
    {
        var usuario = new Usuario
        {
            Nombre = "Cuenta legacy",
            Correo = "legacy@example.com",
            FechaRegistro = DateTime.UtcNow,
        };
        usuario.ContrasenaHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>()
            .HashPassword(usuario, "ClaveSegura123");
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var service = new AuthService(_db, _config);
        var respuesta = await service.LoginAsync(new LoginRequest("legacy@example.com", "ClaveSegura123"));

        Assert.Null(respuesta);
        // La política bloquea el acceso pero conserva el registro y sus datos.
        Assert.True(await _db.Usuarios.AnyAsync(u => u.IdUsuario == usuario.IdUsuario));
    }

    // ---------- Avisos de correo del sistema de notificaciones ----------

    [Fact]
    public async Task Login_Exitoso_DisparaAvisoDeAccesoAlCorreoDelUsuario()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        usuario.ContrasenaHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>()
            .HashPassword(usuario, "ClaveSegura123");
        await _db.SaveChangesAsync();

        var email = new EmailRegistrador();
        var controller = new AuthController(CrearAuthService(), _db, email, _config, new GoogleNonceStore());
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var resultado = await controller.Login(new LoginRequest("ana@gmail.com", "ClaveSegura123"));

        Assert.IsType<OkObjectResult>(resultado.Result);
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("ana@gmail.com", enviado.Destino);
        Assert.Equal("aviso-acceso", enviado.Asunto);
    }

    [Fact]
    public async Task Login_CredencialesInvalidas_NoDisparaAvisoDeAcceso()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        // Hash real de otra contraseña: VerifyHashedPassword debe fallar limpio.
        usuario.ContrasenaHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>()
            .HashPassword(usuario, "ClaveSegura123");
        await _db.SaveChangesAsync();

        var email = new EmailRegistrador();
        var controller = new AuthController(CrearAuthService(), _db, email, _config, new GoogleNonceStore());
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var resultado = await controller.Login(new LoginRequest("ana@gmail.com", "ClaveIncorrecta123"));

        Assert.IsType<UnauthorizedObjectResult>(resultado.Result);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task CambiarContrasena_Exitoso_DisparaAvisoPorCorreo()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>();
        usuario.ContrasenaHash = hasher.HashPassword(usuario, "ClaveSegura123");
        await _db.SaveChangesAsync();

        var email = new EmailRegistrador();
        var controller = new UsuariosController(_db, CrearAuthService(), email);
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var resultado = await controller.CambiarContrasena(new CambiarContrasenaRequest
        {
            ContrasenaActual = "ClaveSegura123",
            NuevaContrasena = "NuevaClave456",
            ConfirmarNueva = "NuevaClave456",
        });

        Assert.IsType<NoContentResult>(resultado);
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("ana@gmail.com", enviado.Destino);
        Assert.Equal("aviso-cambio-contrasena", enviado.Asunto);
    }

    [Fact]
    public async Task CambiarContrasena_ClaveActualIncorrecta_NoDisparaAviso()
    {
        var usuario = await CrearUsuarioSinHogarAsync();
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>();
        usuario.ContrasenaHash = hasher.HashPassword(usuario, "ClaveSegura123");
        await _db.SaveChangesAsync();

        var email = new EmailRegistrador();
        var controller = new UsuariosController(_db, CrearAuthService(), email);
        AsignarClaims(controller, usuario.IdUsuario, idHogarClaim: 0);

        var resultado = await controller.CambiarContrasena(new CambiarContrasenaRequest
        {
            ContrasenaActual = "ClaveErronea123",
            NuevaContrasena = "NuevaClave456",
            ConfirmarNueva = "NuevaClave456",
        });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(email.Enviados);
    }
}

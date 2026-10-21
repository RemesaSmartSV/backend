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
/// Pruebas de la búsqueda de familias por nombre y de la solicitud de unión
/// identificada por idHogar. Cubre también que las escrituras que dependen de
/// hogar devuelvan 400 (y no 500 por la FK NOT NULL) cuando el usuario no
/// tiene familia.
/// </summary>
public class BusquedaFamiliaTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public BusquedaFamiliaTests()
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
        public Task<bool> EnviarResultadoSolicitudUnionAsync(string correoDestino, string nombreSolicitante, string nombreFamilia, bool aceptada)
        { Enviados.Add((correoDestino, "resultado-solicitud-union")); return Task.FromResult(true); }
        public Task<bool> EnviarResultadoInvitacionAsync(string correoDestino, string nombreFamilia, string nombreInvitado, bool aceptada)
        { Enviados.Add((correoDestino, "resultado-invitacion")); return Task.FromResult(true); }
        public Task<bool> EnviarMovimientoFamiliaAsync(string correoDestino, string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
        { Enviados.Add((correoDestino, "movimiento-familia")); return Task.FromResult(true); }
        public Task<bool> EnviarUnionFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
        { Enviados.Add((correoDestino, "union-familia")); return Task.FromResult(true); }
        public Task<bool> EnviarSalidaFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
        { Enviados.Add((correoDestino, "salida-familia")); return Task.FromResult(true); }
    }

    /// <summary>Asigna los claims de prueba a un controller ya construido.</summary>
    private static void AsignarClaims(ControllerBase controller, int idUsuario, int? idHogarClaim)
    {
        var claims = new List<Claim> { new("idUsuario", idUsuario.ToString()) };
        if (idHogarClaim is not null)
            claims.Add(new("idHogar", idHogarClaim.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private HogaresController Hogares(int idUsuario, int? idHogarClaim)
    {
        var controller = new HogaresController(_db, new AuthService(_db, _config));
        AsignarClaims(controller, idUsuario, idHogarClaim);
        return controller;
    }

    private SolicitudesUnionController Solicitudes(int idUsuario, int? idHogarClaim, EmailRegistrador? email = null)
    {
        var controller = new SolicitudesUnionController(_db, email ?? new EmailRegistrador());
        AsignarClaims(controller, idUsuario, idHogarClaim);
        return controller;
    }

    private CategoriasController Categorias(int idUsuario, int? idHogarClaim)
    {
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, idUsuario, idHogarClaim);
        return controller;
    }

    private MetasAhorroController Metas(int idUsuario, int? idHogarClaim)
    {
        var controller = new MetasAhorroController(_db);
        AsignarClaims(controller, idUsuario, idHogarClaim);
        return controller;
    }

    /// <summary>Usuario con la membresia vigente indicada en base de datos.</summary>
    private Usuario SembrarUsuario(int idUsuario, int? idHogar, string correo)
    {
        var usuario = new Usuario
        {
            IdUsuario = idUsuario,
            IdHogar = idHogar,
            Nombre = "Usuario",
            Correo = correo,
            ContrasenaHash = "x",
            Rol = idHogar is null ? null : "Miembro",
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        _db.SaveChanges();
        return usuario;
    }

    private Hogar SembrarHogar(int idHogar, string nombre)
    {
        var hogar = new Hogar { IdHogar = idHogar, NombreFamiliar = nombre };
        _db.Hogares.Add(hogar);
        _db.SaveChanges();
        return hogar;
    }

    // ------------------------------------------------------------------
    // Buscar por nombre
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuscarPorNombre_NoExponeCodigoPublico()
    {
        SembrarHogar(1, "Familia Rivera");
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorNombre("rivera");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);

        Assert.Contains("idHogar", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nombreFamiliar", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CodigoPublico", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RS-FAM-", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuscarPorNombre_MatcheaPorSubcadenaSinMayusculasNiAcentos()
    {
        SembrarHogar(1, "Familia Rivera");
        SembrarHogar(2, "Los Hernández");
        var controller = Hogares(1, null);

        // Mayusculas y acento en la base; la query viene en minusculas y sin tilde.
        var result = await controller.BuscarPorNombre("HERNANDEZ");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrados = Assert.IsAssignableFrom<IEnumerable<HogarResultadoBusquedaDto>>(ok.Value);
        var hogar = Assert.Single(encontrados);
        Assert.Equal(2, hogar.IdHogar);
    }

    [Fact]
    public async Task BuscarPorNombre_EmpiezaCon_VieneAntesQueContiene()
    {
        SembrarHogar(1, "Familia Rivera Norte"); // solo contiene "rivera"
        SembrarHogar(2, "Rivera Família");       // empieza con "rivera"
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorNombre("rivera");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrados = Assert.IsAssignableFrom<IEnumerable<HogarResultadoBusquedaDto>>(ok.Value).ToList();

        Assert.Equal(2, encontrados.Count);
        Assert.Equal(new[] { 2, 1 }, encontrados.Select(h => h.IdHogar).ToArray());
    }

    [Fact]
    public async Task BuscarPorNombre_LimiteDeDiezResultados()
    {
        for (var i = 1; i <= 15; i++)
            SembrarHogar(i, $"Familia Rivera {i:D2}");

        var controller = Hogares(1, null);

        var result = await controller.BuscarPorNombre("rivera");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrados = Assert.IsAssignableFrom<IEnumerable<HogarResultadoBusquedaDto>>(ok.Value);
        Assert.Equal(10, encontrados.Count());
    }

    [Fact]
    public async Task BuscarPorNombre_TerminoCorto_RetornaBadRequest()
    {
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorNombre("ab");

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("3 caracteres", bad.Value?.ToString());
    }

    [Fact]
    public async Task BuscarPorNombre_SinCoincidencias_RetornaListaVacia()
    {
        SembrarHogar(1, "Familia Rivera");
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorNombre("zzzzzzz");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrados = Assert.IsAssignableFrom<IEnumerable<HogarResultadoBusquedaDto>>(ok.Value);
        Assert.Empty(encontrados);
    }

    // ------------------------------------------------------------------
    // Solicitud de union por idHogar
    // ------------------------------------------------------------------

    [Fact]
    public async Task CrearSolicitud_PorIdHogar_NoExigeCodigo()
    {
        SembrarHogar(7, "Familia Rivera");
        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");
        SembrarUsuario(2, idHogar: 7, "admin@gmail.com");
        _db.Usuarios.First(u => u.IdUsuario == 2).Rol = "Admin";
        _db.SaveChanges();

        var controller = Solicitudes(1, null);

        var result = await controller.Crear(new CrearSolicitudUnionRequest { IdHogar = 7 });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var solicitud = Assert.IsType<SolicitudUnionPropiaDto>(created.Value);
        Assert.Equal("Familia Rivera", solicitud.NombreFamilia);
        Assert.Equal(SolicitudUnion.Pendiente, solicitud.Estado);
    }

    [Fact]
    public async Task CrearSolicitud_PorCodigo_SigueFuncionando()
    {
        var hogar = SembrarHogar(7, "Familia Rivera");
        hogar.CodigoPublico = "RS-FAM-ABCDEFGHJKLMNPQR";
        _db.SaveChanges();

        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");
        SembrarUsuario(2, idHogar: 7, "admin@gmail.com");
        _db.Usuarios.First(u => u.IdUsuario == 2).Rol = "Admin";
        _db.SaveChanges();

        var controller = Solicitudes(1, null);

        var result = await controller.Crear(new CrearSolicitudUnionRequest
        {
            CodigoPublico = "RS-FAM-ABCDEFGHJKLMNPQR"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var solicitud = Assert.IsType<SolicitudUnionPropiaDto>(created.Value);
        Assert.Equal("Familia Rivera", solicitud.NombreFamilia);
        Assert.Equal("RS-FAM-ABCDEFGHJKLMNPQR", solicitud.CodigoPublico);
    }

    [Fact]
    public async Task CrearSolicitud_ConAmbosCampos_RetornaBadRequest()
    {
        SembrarHogar(7, "Familia Rivera");
        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");

        var controller = Solicitudes(1, null);

        var result = await controller.Crear(new CrearSolicitudUnionRequest
        {
            CodigoPublico = "RS-FAM-ABCDEFGHJKLMNPQR",
            IdHogar = 7
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("no ambos", bad.Value?.ToString());
    }

    [Fact]
    public async Task CrearSolicitud_SinNingunCampo_RetornaBadRequest()
    {
        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");

        var controller = Solicitudes(1, null);

        var result = await controller.Crear(new CrearSolicitudUnionRequest());

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("codigoPublico", bad.Value?.ToString());
    }

    [Fact]
    public async Task AceptarSolicitud_NotificaPorCorreoAlSolicitante()
    {
        SembrarHogar(7, "Familia Rivera");
        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");
        SembrarUsuario(2, idHogar: 7, "admin@gmail.com");
        _db.Usuarios.First(u => u.IdUsuario == 2).Rol = "Admin";
        var solicitud = new SolicitudUnion { IdHogar = 7, IdUsuario = 1, Estado = SolicitudUnion.Pendiente };
        _db.SolicitudesUnion.Add(solicitud);
        _db.SaveChanges();

        var email = new EmailRegistrador();
        var controller = Solicitudes(2, 7, email);

        var result = await controller.Aceptar(solicitud.IdSolicitud);

        Assert.IsType<NoContentResult>(result);
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("solicitante@gmail.com", enviado.Destino);
        Assert.Equal("resultado-solicitud-union", enviado.Asunto);
    }

    [Fact]
    public async Task RechazarSolicitud_NotificaPorCorreoAlSolicitante()
    {
        SembrarHogar(7, "Familia Rivera");
        SembrarUsuario(1, idHogar: null, "solicitante@gmail.com");
        SembrarUsuario(2, idHogar: 7, "admin@gmail.com");
        _db.Usuarios.First(u => u.IdUsuario == 2).Rol = "Admin";
        var solicitud = new SolicitudUnion { IdHogar = 7, IdUsuario = 1, Estado = SolicitudUnion.Pendiente };
        _db.SolicitudesUnion.Add(solicitud);
        _db.SaveChanges();

        var email = new EmailRegistrador();
        var controller = Solicitudes(2, 7, email);

        var result = await controller.Rechazar(solicitud.IdSolicitud);

        Assert.IsType<NoContentResult>(result);
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("solicitante@gmail.com", enviado.Destino);
        Assert.Equal("resultado-solicitud-union", enviado.Asunto);
    }

    // ------------------------------------------------------------------
    // Buscar por código (formato vigente de 6 dígitos y legacy RS-FAM-)
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuscarPorCodigo_ConSeisDigitos_EncuentraElHogar()
    {
        var hogar = SembrarHogar(1, "Familia Rivera");
        hogar.CodigoPublico = "048293";
        _db.SaveChanges();
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorCodigo("048293");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrado = Assert.IsType<HogarBusquedaDto>(ok.Value);
        Assert.Equal("048293", encontrado.CodigoPublico);
        Assert.Equal("Familia Rivera", encontrado.NombreFamiliar);
    }

    [Fact]
    public async Task BuscarPorCodigo_ConFormatoLegacy_EncuentraElHogar()
    {
        // Compatible con los códigos hex históricos de la BD, que usan el
        // alfabeto laxo A-Z/0-9 y no el estricto del viejo generador.
        var hogar = SembrarHogar(1, "Familia Rivera");
        hogar.CodigoPublico = "RS-FAM-83656A6F88F54631";
        _db.SaveChanges();
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorCodigo("rs-fam-83656a6f88f54631");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var encontrado = Assert.IsType<HogarBusquedaDto>(ok.Value);
        Assert.Equal("RS-FAM-83656A6F88F54631", encontrado.CodigoPublico);
        Assert.Equal("Familia Rivera", encontrado.NombreFamiliar);
    }

    [Fact]
    public async Task BuscarPorCodigo_FormatoInvalido_RetornaBadRequest()
    {
        var controller = Hogares(1, null);

        var result = await controller.BuscarPorCodigo("12345");

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("formato válido", bad.Value?.ToString());
    }

    // ------------------------------------------------------------------
    // Escrituras sin hogar deben dar 400, nunca 500
    // ------------------------------------------------------------------

    [Fact]
    public async Task CrearCategoria_SinHogar_RetornaBadRequest()
    {
        // El usuario existe pero no tiene familia: la FK hacia Hogares es NOT NULL,
        // asi que sin esta validacion el SaveChanges reventaba en 500.
        SembrarUsuario(1, idHogar: null, "sinfamilia@gmail.com");

        var controller = Categorias(1, null);

        var result = await controller.Create(new Categoria
        {
            Nombre = "Alimentación",
            Tipo = "Gasto"
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("familia", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _db.Categorias.ToListAsync());
    }

    [Fact]
    public async Task CrearCategoria_ConHogar_SigueCreando()
    {
        SembrarUsuario(1, idHogar: 1, "conhogar@gmail.com");
        SembrarHogar(1, "Familia Rivera");

        var controller = Categorias(1, 1);

        var result = await controller.Create(new Categoria
        {
            Nombre = "Alimentación",
            Tipo = "Gasto"
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var creada = Assert.Single(await _db.Categorias.ToListAsync());
        Assert.Equal(1, creada.IdHogar);
    }

    [Fact]
    public async Task CrearMeta_SinHogar_RetornaBadRequest()
    {
        SembrarUsuario(1, idHogar: null, "sinfamilia@gmail.com");

        var controller = Metas(1, null);

        var result = await controller.Create(new MetaAhorro
        {
            Titulo = "Fondo de emergencia",
            MontoObjetivo = 1000,
            FechaLimite = DateTime.UtcNow.AddDays(30)
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("familia", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _db.MetasAhorro.ToListAsync());
    }

    [Fact]
    public async Task CrearMeta_ConHogar_SigueCreando()
    {
        SembrarUsuario(1, idHogar: 1, "conhogar@gmail.com");
        SembrarHogar(1, "Familia Rivera");

        var controller = Metas(1, 1);

        var result = await controller.Create(new MetaAhorro
        {
            Titulo = "Fondo de emergencia",
            MontoObjetivo = 1000,
            FechaLimite = DateTime.UtcNow.AddDays(30)
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var creada = Assert.Single(await _db.MetasAhorro.ToListAsync());
        Assert.Equal(1, creada.IdHogar);
    }
}

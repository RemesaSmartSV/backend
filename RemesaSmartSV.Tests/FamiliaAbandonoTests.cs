using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas de <see cref="UsuariosController.AbandonarFamilia"/> y del borrado
/// de hogares. Cubre el requisito §6.3 Caso A: cuando se va el último miembro
/// (o se intenta borrar la familia), el hogar y sus datos financieros se conservan.
/// </summary>
public class FamiliaAbandonoTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public FamiliaAbandonoTests()
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

    // Solo para inyectar IEmailService: abandonar familia no envía correos.
    private class EmailStub : IEmailService
    {
        public Task<bool> EnviarInvitacionFamiliarAsync(string a, string b, string c, string d) => Task.FromResult(true);
        public Task<bool> EnviarResetContrasenaAsync(string a, string b) => Task.FromResult(true);
        public Task<bool> EnviarAlertaPresupuestoAsync(string a, string b, string c, decimal d, decimal e, decimal f) => Task.FromResult(true);
        public Task<bool> EnviarPresupuestoExcedidoAsync(string a, string b, string c, decimal d, decimal e) => Task.FromResult(true);
        public Task<bool> EnviarNotificacionCuentaAsync(string a, string b, string c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoAccesoAsync(string a, DateTime b, string? c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string a, DateTime b) => Task.FromResult(true);
    }

    private UsuariosController CrearController(int idUsuario)
    {
        var controller = new UsuariosController(_db, new AuthService(_db, _config), new EmailStub());

        var claims = new List<Claim> { new("idUsuario", idUsuario.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    /// <summary>Hogar con un miembro y datos financieros reales de todas las tablas hijas.</summary>
    private (Hogar hogar, Usuario unicoMiembro) SembrarHogarConDatos(int idHogar = 1)
    {
        var hogar = new Hogar { IdHogar = idHogar, NombreFamiliar = "Familia Prueba" };
        var unicoMiembro = new Usuario
        {
            IdUsuario = 1,
            IdHogar = idHogar,
            Nombre = "Único",
            Correo = "unico@gmail.com",
            ContrasenaHash = "x",
            Rol = "Admin",
            FechaRegistro = DateTime.UtcNow
        };

        _db.Hogares.Add(hogar);
        _db.Usuarios.Add(unicoMiembro);

        var categoria = new Categoria { IdCategoria = 1, IdHogar = idHogar, Nombre = "Comida", Tipo = "Gasto" };
        _db.Categorias.Add(categoria);

        var meta = new MetaAhorro
        {
            IdMeta = 1,
            IdHogar = idHogar,
            IdUsuario = unicoMiembro.IdUsuario,
            Titulo = "Vacaciones",
            MontoObjetivo = 1000,
            MontoActual = 100,
            FechaLimite = DateTime.UtcNow.AddDays(30),
            Estado = "En progreso"
        };
        _db.MetasAhorro.Add(meta);

        _db.Aportes.Add(new AporteMeta
        {
            IdAporte = 1,
            IdMeta = 1,
            IdHogar = idHogar,
            IdUsuario = unicoMiembro.IdUsuario,
            Monto = 100,
            Fecha = DateTime.UtcNow
        });

        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1,
            IdHogar = idHogar,
            IdUsuario = unicoMiembro.IdUsuario,
            IdCategoria = 1,
            Monto = 250,
            Fecha = DateTime.UtcNow,
            Tipo = "Gasto",
            Descripcion = "Mercado"
        });

        _db.Presupuestos.Add(new Presupuesto
        {
            IdPresupuesto = 1,
            IdHogar = idHogar,
            IdCategoria = 1,
            MontoLimite = 500,
            MesAnio = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        _db.SaveChanges();
        return (hogar, unicoMiembro);
    }

    [Fact]
    public async Task AbandonarFamilia_UltimoMiembro_ConservaHogarYDatosFinancieros()
    {
        var (hogar, unico) = SembrarHogarConDatos();
        var controller = CrearController(unico.IdUsuario);

        var result = await controller.AbandonarFamilia();

        var ok = Assert.IsType<OkObjectResult>(result.Result);

        // El hogar sigue existiendo, vacio de personas pero intacto.
        Assert.NotNull(await _db.Hogares.FindAsync(hogar.IdHogar));

        // Todos los datos financieros se conservan (§6.3 Caso A).
        Assert.Single(await _db.Categorias.Where(c => c.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.MetasAhorro.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Aportes.Where(a => a.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Movimientos.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Presupuestos.Where(p => p.IdHogar == hogar.IdHogar).ToListAsync());

        // El usuario SOBREVIVE (su IdHogar es nullable) pero queda desvinculado.
        var usuario = await _db.Usuarios.FindAsync(unico.IdUsuario);
        Assert.NotNull(usuario);
        Assert.Null(usuario!.IdHogar);
        Assert.Null(usuario.Rol);
        Assert.Equal(unico.Correo, usuario.Correo);

        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task AbandonarFamilia_ConOtrosMiembrosNoEliminaHogarNiDatos()
    {
        var (hogar, unico) = SembrarHogarConDatos();

        var segundo = new Usuario
        {
            IdUsuario = 2,
            IdHogar = hogar.IdHogar,
            Nombre = "Segundo",
            Correo = "segundo@gmail.com",
            ContrasenaHash = "x",
            Rol = "Miembro",
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(segundo);
        await _db.SaveChangesAsync();

        var controller = CrearController(unico.IdUsuario);

        var result = await controller.AbandonarFamilia();

        Assert.IsType<OkObjectResult>(result.Result);

        // La familia sigue viva con su segundo miembro y todos sus datos.
        Assert.NotNull(await _db.Hogares.FindAsync(hogar.IdHogar));
        Assert.Single(await _db.Usuarios.Where(u => u.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Movimientos.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.MetasAhorro.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());

        // Solo se desvinculo quien se fue.
        var salio = await _db.Usuarios.FindAsync(unico.IdUsuario);
        Assert.Null(salio!.IdHogar);
        Assert.Null(salio.Rol);

        var quedo = await _db.Usuarios.FindAsync(segundo.IdUsuario);
        Assert.Equal(hogar.IdHogar, quedo!.IdHogar);
    }

    [Fact]
    public async Task AbandonarFamilia_UltimoAdminYaNoEstaBloqueado()
    {
        // Regresion: antes el ultimo Admin no podia irse. Ahora si.
        var (hogar, admin) = SembrarHogarConDatos();
        Assert.Equal("Admin", admin.Rol);

        var controller = CrearController(admin.IdUsuario);

        var result = await controller.AbandonarFamilia();

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(await _db.Hogares.FindAsync(hogar.IdHogar));
    }

    [Fact]
    public async Task AbandonarFamilia_SinHogar_RetornaBadRequest()
    {
        var huérfano = new Usuario
        {
            IdUsuario = 1,
            IdHogar = null,
            Nombre = "Sin familia",
            Correo = "sinfamilia@gmail.com",
            ContrasenaHash = "x",
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(huérfano);
        await _db.SaveChangesAsync();

        var controller = CrearController(huérfano.IdUsuario);

        var result = await controller.AbandonarFamilia();

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("No perteneces a ninguna familia", bad.Value?.ToString());
    }

    [Fact]
    public async Task AbandonarFamilia_UsuarioInexistente_RetornaNotFound()
    {
        var controller = CrearController(999);

        var result = await controller.AbandonarFamilia();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task HogaresController_Delete_ConDatosFinancieros_ReturnsConflict()
    {
        var (hogar, _) = SembrarHogarConDatos();

        var controller = new HogaresController(_db, new AuthService(_db, _config));
        var claims = new List<Claim>
        {
            new("idUsuario", "1"),
            new("idHogar", hogar.IdHogar.ToString())
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        var result = await controller.Delete(hogar.IdHogar);

        // El borrado se rechaza: la familia tiene datos financieros que se conservan.
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("datos financieros", conflict.Value?.ToString());

        // Ni el hogar ni sus datos se eliminaron.
        Assert.NotNull(await _db.Hogares.FindAsync(hogar.IdHogar));
        Assert.Single(await _db.Categorias.Where(c => c.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.MetasAhorro.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Aportes.Where(a => a.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Movimientos.Where(m => m.IdHogar == hogar.IdHogar).ToListAsync());
        Assert.Single(await _db.Presupuestos.Where(p => p.IdHogar == hogar.IdHogar).ToListAsync());
    }
}

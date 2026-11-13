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
/// Cubre las correcciones de seguridad: roles en categorías, protección de
/// categorías del sistema, protección de datos al eliminar, origen de movimientos
/// no falsificable, inyección de fórmulas en CSV, idempotencia de aportes por
/// hogar y tips globales limitados a una lista blanca.
/// </summary>
public class SeguridadCorreccionesTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    private class EmailStub : IEmailService
    {
        public Task<bool> EnviarInvitacionFamiliarAsync(string a, string b, string c, string d) => Task.FromResult(true);
        public Task<bool> EnviarResetContrasenaAsync(string a, string b) => Task.FromResult(true);
        public Task<bool> EnviarAlertaPresupuestoAsync(string a, string b, string c, decimal d, decimal e, decimal f) => Task.FromResult(true);
        public Task<bool> EnviarPresupuestoExcedidoAsync(string a, string b, string c, decimal d, decimal e) => Task.FromResult(true);
        public Task<bool> EnviarNotificacionCuentaAsync(string a, string b, string c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoAccesoAsync(string a, DateTime b, string? c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string a, DateTime b) => Task.FromResult(true);
        public Task<bool> EnviarResultadoSolicitudUnionAsync(string a, string b, string c, bool d) => Task.FromResult(true);
        public Task<bool> EnviarResultadoInvitacionAsync(string a, string b, string c, bool d) => Task.FromResult(true);
        public Task<bool> EnviarMovimientoFamiliaAsync(string a, string b, string c, string d, decimal e, string? f, DateTime g) => Task.FromResult(true);
        public Task<bool> EnviarUnionFamiliaAsync(string a, string b, string c) => Task.FromResult(true);
        public Task<bool> EnviarSalidaFamiliaAsync(string a, string b, string c) => Task.FromResult(true);
    }

    public SeguridadCorreccionesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static void AsignarClaims(ControllerBase controller, int idUsuario, int idHogar, string rol)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString()),
            new(ClaimTypes.Role, rol)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    private Hogar SembrarHogar(int idHogar, string nombre)
    {
        var hogar = new Hogar { IdHogar = idHogar, NombreFamiliar = nombre };
        _db.Hogares.Add(hogar);
        _db.SaveChanges();
        return hogar;
    }

    private Usuario SembrarUsuario(int idUsuario, int idHogar, string correo, string rol)
    {
        var usuario = new Usuario
        {
            IdUsuario = idUsuario,
            IdHogar = idHogar,
            Nombre = "Usuario",
            Correo = correo,
            ContrasenaHash = "x",
            Rol = rol,
            FechaRegistro = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        _db.SaveChanges();
        return usuario;
    }

    private Categoria SembrarCategoria(int idCategoria, int idHogar, string nombre = "General", bool esSistema = false)
    {
        var categoria = new Categoria
        {
            IdCategoria = idCategoria,
            IdHogar = idHogar,
            Nombre = nombre,
            Tipo = "Gasto",
            EsSistema = esSistema
        };
        _db.Categorias.Add(categoria);
        _db.SaveChanges();
        return categoria;
    }

    // ------------------------------------------------------------------
    // Categorías: rol Admin y protección de las del sistema
    // ------------------------------------------------------------------

    [Fact]
    public async Task Categoria_MiembroNoPuedeActualizar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarCategoria(1, 1, "Supermercado");
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Update(1, new Categoria { Nombre = "Hackeada", Tipo = "Gasto" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Categoria_MiembroNoPuedeEliminar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarCategoria(1, 1, "Supermercado");
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Delete(1);

        Assert.IsType<ForbidResult>(result);
        Assert.NotNull(await _db.Categorias.FindAsync(1));
    }

    [Fact]
    public async Task Categoria_AdminPuedeActualizar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarCategoria(1, 1, "Supermercado");
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Update(1, new Categoria { Nombre = "Supermercado 2", Tipo = "Gasto" });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Supermercado 2", (await _db.Categorias.FindAsync(1))!.Nombre);
    }

    [Fact]
    public async Task Categoria_CreadaPorElUsuarioNuncaEsSistema()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Create(new Categoria
        {
            Nombre = "Creada por mi",
            Tipo = "Gasto",
            EsSistema = true
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var creada = await _db.Categorias.SingleAsync(c => c.Nombre == "Creada por mi");
        Assert.False(creada.EsSistema);
    }

    [Fact]
    public async Task Categoria_DelSistema_NoSePuedeEliminar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarCategoria(1, 1, "Alimentación", esSistema: true);
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Delete(1);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("sistema", conflict.Value?.ToString());
    }

    [Fact]
    public async Task Categoria_ConMovimientos_NoSePuedeEliminar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        var categoria = SembrarCategoria(1, 1, "Supermercado");
        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = categoria.IdCategoria,
            Monto = 10m,
            Fecha = DateTime.UtcNow,
            Tipo = "Gasto"
        });
        _db.SaveChanges();

        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Delete(1);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(await _db.Categorias.FindAsync(1));
    }

    [Fact]
    public async Task Categoria_SinUso_AdminPuedeEliminar()
    {
        SembrarHogar(1, "Familia Test");
        SembrarCategoria(1, 1, "Sin uso");
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Categorias.FindAsync(1));
    }

    // ------------------------------------------------------------------
    // OrigenEmisora no falsificable
    // ------------------------------------------------------------------

    [Fact]
    public async Task Movimiento_CrearConOrigenAporte_Rechazado()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        SembrarCategoria(1, 1, "Otros ahorros");
        var controller = new MovimientosController(_db, new EmailStub());
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Create(new Movimiento
        {
            IdCategoria = 1,
            Monto = 100m,
            Fecha = DateTime.UtcNow,
            Tipo = "Ahorro",
            OrigenEmisora = "aporte:999"
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("origen", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Movimiento_CrearConOrigenLegitimo_Aceptado()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        SembrarCategoria(1, 1, "Otros ahorros");
        var controller = new MovimientosController(_db, new EmailStub());
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Create(new Movimiento
        {
            IdCategoria = 1,
            Monto = 100m,
            Fecha = DateTime.UtcNow,
            Tipo = "Ahorro",
            OrigenEmisora = "Transferencia"
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    // ------------------------------------------------------------------
    // Inyección de fórmulas en CSV
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportarCsv_DescripcionMaliciosa_NoQuedaComoFormula()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        SembrarCategoria(1, 1, "General");
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1,
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = 1,
            Monto = 5m,
            Fecha = new DateTime(2026, 9, 15),
            Tipo = "Gasto",
            Descripcion = "=cmd|'/C calc'!A0"
        });
        _db.SaveChanges();

        var controller = new MovimientosController(_db, new EmailStub());
        AsignarClaims(controller, 1, 1, "Miembro");

        var result = await controller.Exportar("csv");

        var file = Assert.IsType<FileContentResult>(result);
        var contenido = System.Text.Encoding.UTF8.GetString(file.FileContents);

        Assert.DoesNotContain("\"=cmd", contenido);
        Assert.Contains("\"'=cmd", contenido);
    }

    // ------------------------------------------------------------------
    // Idempotencia de aportes por hogar
    // ------------------------------------------------------------------

    [Fact]
    public async Task Aportes_MismaClaveEnOtroHogar_NoCompiten()
    {
        SembrarHogar(1, "Familia Uno");
        SembrarHogar(2, "Familia Dos");
        SembrarUsuario(1, 1, "ana@gmail.com", "Miembro");
        SembrarUsuario(2, 2, "juan@gmail.com", "Miembro");

        var meta1 = new MetaAhorro { IdHogar = 1, Titulo = "Meta 1", MontoObjetivo = 1000, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso" };
        var meta2 = new MetaAhorro { IdHogar = 2, Titulo = "Meta 2", MontoObjetivo = 1000, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso" };
        _db.MetasAhorro.AddRange(meta1, meta2);
        _db.SaveChanges();

        var clave = Guid.NewGuid();

        var controller1 = new AportesController(_db);
        AsignarClaims(controller1, 1, 1, "Miembro");
        var r1 = await controller1.Create(new CrearAporteRequest { IdMeta = meta1.IdMeta, Monto = 50, IdempotencyKey = clave });

        var controller2 = new AportesController(_db);
        AsignarClaims(controller2, 2, 2, "Miembro");
        var r2 = await controller2.Create(new CrearAporteRequest { IdMeta = meta2.IdMeta, Monto = 75, IdempotencyKey = clave });

        Assert.IsType<CreatedAtActionResult>(r1.Result);
        Assert.IsType<CreatedAtActionResult>(r2.Result);
        Assert.Equal(2, await _db.Aportes.CountAsync());
    }

    // ------------------------------------------------------------------
    // Tips globales con lista blanca
    // ------------------------------------------------------------------

    private static IConfiguration ConfigConAdmin(string correo)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TIPS_ADMIN_EMAILS"] = correo
            })
            .Build();

    [Fact]
    public async Task Tips_UsuarioSinListaBlanca_NoPuedeCrear()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "admin@gmail.com", "Admin");
        var controller = new TipsFinancierosController(_db, ConfigConAdmin("otro@ejemplo.com"));
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Create(new EducacionFinanciera
        {
            Titulo = "Tip",
            Contenido = "Contenido"
        });

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Tips_UsuarioEnListaBlanca_PuedeCrear()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "admin@gmail.com", "Admin");
        var controller = new TipsFinancierosController(_db, ConfigConAdmin("admin@gmail.com"));

        var claims = new List<Claim>
        {
            new("idUsuario", "1"),
            new("idHogar", "1"),
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Email, "admin@gmail.com")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        var result = await controller.Create(new EducacionFinanciera
        {
            Titulo = "Tip",
            Contenido = "Contenido"
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task Tips_ListaBlancaVacia_NadiePuedeEscribir()
    {
        SembrarHogar(1, "Familia Test");
        SembrarUsuario(1, 1, "admin@gmail.com", "Admin");
        var controller = new TipsFinancierosController(_db, ConfigConAdmin(string.Empty));
        AsignarClaims(controller, 1, 1, "Admin");

        var result = await controller.Delete(1);

        Assert.IsType<ForbidResult>(result);
    }
}

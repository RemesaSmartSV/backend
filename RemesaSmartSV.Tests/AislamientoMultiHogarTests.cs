using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Aislamiento multiusuario/multihogar: un usuario de un hogar no puede leer ni
/// modificar los recursos de otro hogar, en ningún endpoint CRUD financiero.
/// </summary>
public class AislamientoMultiHogarTests : IDisposable
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

    public AislamientoMultiHogarTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

        // Dos hogares con un usuario cada uno y datos financieros propios.
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Uno" });
        _db.Hogares.Add(new Hogar { IdHogar = 2, NombreFamiliar = "Familia Dos" });
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1, IdHogar = 1, Nombre = "Ana", Correo = "ana@gmail.com",
            ContrasenaHash = "x", Rol = "Miembro", FechaRegistro = DateTime.UtcNow
        });
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 2, IdHogar = 2, Nombre = "Juan", Correo = "juan@gmail.com",
            ContrasenaHash = "x", Rol = "Miembro", FechaRegistro = DateTime.UtcNow
        });
        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Comida", Tipo = "Gasto" });
        _db.Categorias.Add(new Categoria { IdCategoria = 2, IdHogar = 2, Nombre = "Viajes", Tipo = "Gasto" });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1, IdHogar = 1, IdUsuario = 1, IdCategoria = 1,
            Monto = 10m, Fecha = DateTime.UtcNow, Tipo = "Gasto"
        });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 2, IdHogar = 2, IdUsuario = 2, IdCategoria = 2,
            Monto = 20m, Fecha = DateTime.UtcNow, Tipo = "Gasto"
        });
        _db.MetasAhorro.Add(new MetaAhorro
        {
            IdMeta = 1, IdHogar = 1, Titulo = "Meta uno", MontoObjetivo = 100,
            MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso"
        });
        _db.MetasAhorro.Add(new MetaAhorro
        {
            IdMeta = 2, IdHogar = 2, Titulo = "Meta dos", MontoObjetivo = 200,
            MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso"
        });
        _db.Aportes.Add(new AporteMeta
        {
            IdAporte = 1, IdMeta = 1, IdHogar = 1, IdUsuario = 1,
            Monto = 5m, Fecha = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static void AsignarClaims(ControllerBase controller, int idUsuario, int idHogar)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString())
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    private T ControllerDeHogar<T>(T controller, int idUsuario, int idHogar) where T : ControllerBase
    {
        AsignarClaims(controller, idUsuario, idHogar);
        return controller;
    }

    // --- Movimientos ---

    [Fact]
    public async Task Movimiento_OtroHogar_NoSeLee()
    {
        var controller = ControllerDeHogar(new MovimientosController(_db, new EmailStub()), 1, 1);

        var result = await controller.GetMovimiento(2);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Movimiento_OtroHogar_NoSeModifica()
    {
        var controller = ControllerDeHogar(new MovimientosController(_db, new EmailStub()), 1, 1);

        var result = await controller.Update(2, new Movimiento
        {
            IdCategoria = 2, Monto = 999m, Fecha = DateTime.UtcNow, Tipo = "Gasto"
        });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Movimiento_OtroHogar_NoSeElimina()
    {
        var controller = ControllerDeHogar(new MovimientosController(_db, new EmailStub()), 1, 1);

        var result = await controller.Delete(2);

        Assert.IsType<NotFoundResult>(result);
        Assert.NotNull(await _db.Movimientos.FindAsync(2));
    }

    // --- Categorías ---

    [Fact]
    public async Task Categoria_OtroHogar_NoSeLee()
    {
        var controller = ControllerDeHogar(new CategoriasController(_db), 1, 1);

        var result = await controller.GetCategoria(2);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Categoria_OtroHogar_NoSeModificaNiElimina()
    {
        var controller = ControllerDeHogar(new CategoriasController(_db), 1, 1);
        AsignarClaims(controller, 1, 1);

        var upd = await controller.Update(2, new Categoria { Nombre = "Hackeada", Tipo = "Gasto" });
        var del = await controller.Delete(2);

        // El usuario es Miembro, así que además del filtro por hogar se exige rol Admin.
        Assert.IsType<ForbidResult>(upd);
        Assert.IsType<ForbidResult>(del);
    }

    [Fact]
    public async Task Categoria_OtroHogar_AdminNoPuedeTocarla()
    {
        var controller = new CategoriasController(_db);
        AsignarClaims(controller, 1, 1);
        // Cambiar el rol a Admin requiere un claim de rol.
        var claims = new List<Claim>
        {
            new("idUsuario", "1"),
            new("idHogar", "1"),
            new(ClaimTypes.Role, "Admin")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        var upd = await controller.Update(2, new Categoria { Nombre = "Hackeada", Tipo = "Gasto" });
        var del = await controller.Delete(2);

        Assert.IsType<NotFoundResult>(upd);
        Assert.IsType<NotFoundResult>(del);
    }

    // --- Metas y Aportes ---

    [Fact]
    public async Task Meta_OtroHogar_NoSeLee()
    {
        var controller = ControllerDeHogar(new MetasAhorroController(_db), 1, 1);

        var result = await controller.GetMeta(2);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Aportes_OtroHogar_NoSeListan()
    {
        var controller = ControllerDeHogar(new AportesController(_db), 1, 1);

        var result = await controller.GetAportes(2);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("no existe", bad.Value?.ToString());
    }

    [Fact]
    public async Task Aportes_OtroHogar_NoSeEliminan()
    {
        // El aporte 1 pertenece al hogar 1, pero se pide desde el hogar 2.
        var controller = ControllerDeHogar(new AportesController(_db), 2, 2);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
        Assert.NotNull(await _db.Aportes.FindAsync(1));
    }
}

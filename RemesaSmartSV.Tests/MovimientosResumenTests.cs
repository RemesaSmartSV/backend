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
/// Cubre el resumen del tablero con la regla de ahorro unificado:
/// TotalAhorro suma los AporteMeta del período más los movimientos de tipo
/// "Ahorro" sin marca "aporte:", y Balance = Ingresos - Gastos - Ahorro.
/// </summary>
public class MovimientosResumenTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly MovimientosController _controller;

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

    public MovimientosResumenTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _controller = new MovimientosController(_db, new EmailStub());

        var claims = new List<Claim> { new("idHogar", "1") };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    public void Dispose() => _db.Dispose();

    private async Task SembrarMovimientosAsync()
    {
        _db.Categorias.Add(new Categoria { IdHogar = 1, Nombre = "Salario", Tipo = "Ingreso" });
        _db.Categorias.Add(new Categoria { IdHogar = 1, Nombre = "Alimentación", Tipo = "Gasto" });
        await _db.SaveChangesAsync();

        var ingreso = await _db.Categorias.SingleAsync(c => c.Nombre == "Salario");
        var gasto = await _db.Categorias.SingleAsync(c => c.Nombre == "Alimentación");

        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = ingreso.IdCategoria,
            Monto = 1000m,
            Fecha = new DateTime(2026, 10, 3),
            Tipo = "Ingreso",
        });
        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = gasto.IdCategoria,
            Monto = 400m,
            Fecha = new DateTime(2026, 10, 4),
            Tipo = "Gasto",
        });
        await _db.SaveChangesAsync();
    }

    private async Task<AporteMeta> SembrarAporteAsync(decimal monto, DateTime fecha, bool conMovimiento = false)
    {
        var meta = new MetaAhorro
        {
            IdHogar = 1,
            Titulo = "Meta resumen",
            MontoObjetivo = 1000,
            MontoActual = monto,
            FechaLimite = fecha.AddDays(30),
            Estado = "En progreso",
        };
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();

        var aporte = new AporteMeta { IdMeta = meta.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = monto, Fecha = fecha };
        _db.Aportes.Add(aporte);
        await _db.SaveChangesAsync();

        if (conMovimiento)
        {
            var categoria = new Categoria { IdHogar = 1, Nombre = "Otros ahorros", Tipo = "Ahorro" };
            _db.Categorias.Add(categoria);
            await _db.SaveChangesAsync();

            _db.Movimientos.Add(new Movimiento
            {
                IdHogar = 1,
                IdUsuario = 1,
                IdCategoria = categoria.IdCategoria,
                Monto = monto,
                Fecha = fecha,
                Tipo = "Ahorro",
                OrigenEmisora = $"aporte:{aporte.IdAporte}",
            });
            await _db.SaveChangesAsync();
        }

        return aporte;
    }

    [Fact]
    public async Task GetResumen_DevuelveTotalAhorroYUnBalanceQueYaLoDescuenta()
    {
        await SembrarMovimientosAsync();
        await SembrarAporteAsync(150m, new DateTime(2026, 10, 6));

        var result = await _controller.GetResumen(anio: null, mes: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(400m, dto.TotalGastos);
        Assert.Equal(150m, dto.TotalAhorro);
        Assert.Equal(450m, dto.Balance);
    }

    [Fact]
    public async Task GetResumen_ElAporteNoModificaIngresosNiGastos()
    {
        await SembrarMovimientosAsync();
        await SembrarAporteAsync(150m, new DateTime(2026, 10, 6), conMovimiento: true);

        var result = await _controller.GetResumen(anio: null, mes: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        // Ingresos sigue siendo bruto: el aporte no lo toca.
        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(400m, dto.TotalGastos);
        Assert.Equal(150m, dto.TotalAhorro);
    }

    [Fact]
    public async Task GetResumen_ElAhorroSumaAportesYMovimientosManualesSinDuplicar()
    {
        await SembrarMovimientosAsync();

        // Aporte anterior a la feature: no tiene movimiento asociado.
        await SembrarAporteAsync(80m, new DateTime(2026, 10, 6));

        // Un movimiento de ahorro hecho a mano (sin marca "aporte:") ahora suma al KPI.
        var categoria = new Categoria { IdHogar = 1, Nombre = "Vehículo", Tipo = "Ahorro" };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = categoria.IdCategoria,
            Monto = 120m,
            Fecha = new DateTime(2026, 10, 7),
            Tipo = "Ahorro",
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetResumen(anio: null, mes: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        Assert.Equal(200m, dto.TotalAhorro);
        Assert.Equal(400m, dto.Balance);
    }

    [Fact]
    public async Task GetResumen_ConFiltroDeMes_AplicaLaMismaVentanaAlAhorro()
    {
        await SembrarMovimientosAsync();
        await SembrarAporteAsync(100m, new DateTime(2026, 9, 10));
        await SembrarAporteAsync(50m, new DateTime(2026, 10, 10));

        var result = await _controller.GetResumen(anio: 2026, mes: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(400m, dto.TotalGastos);
        Assert.Equal(50m, dto.TotalAhorro);
        Assert.Equal(550m, dto.Balance);
    }

    [Fact]
    public async Task GetResumen_SinAportes_ElBalanceEsIngresosMenosGastos()
    {
        await SembrarMovimientosAsync();

        var result = await _controller.GetResumen(anio: null, mes: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        Assert.Equal(0m, dto.TotalAhorro);
        Assert.Equal(600m, dto.Balance);
    }

    [Fact]
    public async Task GetResumen_ElAhorroSoloCuentaDentroDeLaVentanaSeleccionada()
    {
        await SembrarMovimientosAsync();
        await SembrarAporteAsync(70m, new DateTime(2026, 11, 2));

        var result = await _controller.GetResumen(anio: 2026, mes: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenDashboardDTO>(ok.Value);

        Assert.Equal(0m, dto.TotalAhorro);
        Assert.Equal(600m, dto.Balance);
    }
}

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
/// Cubre el resumen del tablero por rango de fechas (B1): prelación de
/// fechaInicio/fechaFin sobre anio/mes, año completo cuando viene solo anio,
/// ahorro unificado (aportes + movimientos manuales sin marca "aporte:"),
/// IngresosSalario y el desglose AportesPorMeta.
/// </summary>
public class MovimientosResumenPeriodoTests : IDisposable
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

    public MovimientosResumenPeriodoTests()
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

    private async Task<(Categoria ingreso, Categoria gasto, Categoria ahorro)> SembrarCategoriasAsync()
    {
        var ingreso = new Categoria { IdHogar = 1, Nombre = "Salario", Tipo = "Ingreso" };
        var gasto = new Categoria { IdHogar = 1, Nombre = "Alimentación", Tipo = "Gasto" };
        var ahorro = new Categoria { IdHogar = 1, Nombre = "Otros ahorros", Tipo = "Ahorro" };
        _db.Categorias.AddRange(ingreso, gasto, ahorro);
        await _db.SaveChangesAsync();
        return (ingreso, gasto, ahorro);
    }

    private async Task SembrarMovimientoAsync(int idCategoria, decimal monto, DateTime fecha, string tipo, string? origenEmisora = null)
    {
        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = idCategoria,
            Monto = monto,
            Fecha = fecha,
            Tipo = tipo,
            OrigenEmisora = origenEmisora,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<(MetaAhorro meta, AporteMeta aporte)> SembrarAporteAsync(string titulo, decimal monto, DateTime fecha)
    {
        var meta = new MetaAhorro
        {
            IdHogar = 1,
            Titulo = titulo,
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
        return (meta, aporte);
    }

    private static async Task<ResumenDashboardDTO> ObtenerResumenAsync(MovimientosController controller,
        int? anio = null, int? mes = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
    {
        var result = await controller.GetResumen(anio, mes, fechaInicio, fechaFin);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ResumenDashboardDTO>(ok.Value);
    }

    [Fact]
    public async Task GetResumen_ConRangoDeFechas_SoloCuentaElRango()
    {
        var (ingreso, gasto, _) = await SembrarCategoriasAsync();
        await SembrarMovimientoAsync(ingreso.IdCategoria, 1000m, new DateTime(2026, 10, 5), "Ingreso");
        await SembrarMovimientoAsync(gasto.IdCategoria, 200m, new DateTime(2026, 10, 20), "Gasto");
        await SembrarMovimientoAsync(gasto.IdCategoria, 50m, new DateTime(2026, 9, 1), "Gasto");
        await SembrarMovimientoAsync(gasto.IdCategoria, 70m, new DateTime(2026, 11, 15), "Gasto");

        var dto = await ObtenerResumenAsync(_controller,
            fechaInicio: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            fechaFin: new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(200m, dto.TotalGastos);
        Assert.True(dto.TieneDatos);
    }

    [Fact]
    public async Task GetResumen_SoloAnio_SoloCuentaEseAnio()
    {
        var (ingreso, gasto, _) = await SembrarCategoriasAsync();
        await SembrarMovimientoAsync(ingreso.IdCategoria, 500m, new DateTime(2026, 1, 15), "Ingreso");
        await SembrarMovimientoAsync(ingreso.IdCategoria, 300m, new DateTime(2026, 12, 20), "Ingreso");
        await SembrarMovimientoAsync(gasto.IdCategoria, 400m, new DateTime(2026, 6, 10), "Gasto");
        // Fuera del año consultado: no debe contar.
        await SembrarMovimientoAsync(ingreso.IdCategoria, 999m, new DateTime(2025, 12, 31), "Ingreso");
        await SembrarMovimientoAsync(ingreso.IdCategoria, 888m, new DateTime(2027, 1, 1), "Ingreso");
        await SembrarAporteAsync("Viaje 2026", 100m, new DateTime(2026, 3, 1));
        await SembrarAporteAsync("Viaje 2025", 555m, new DateTime(2025, 5, 1));

        var dto = await ObtenerResumenAsync(_controller, anio: 2026);

        Assert.Equal(800m, dto.TotalIngresos);
        Assert.Equal(400m, dto.TotalGastos);
        Assert.Equal(100m, dto.TotalAhorro);
    }

    [Fact]
    public async Task GetResumen_AnioYMes_SigueComportandoComoAntes()
    {
        var (ingreso, gasto, _) = await SembrarCategoriasAsync();
        await SembrarMovimientoAsync(ingreso.IdCategoria, 1000m, new DateTime(2026, 10, 3), "Ingreso");
        await SembrarMovimientoAsync(gasto.IdCategoria, 400m, new DateTime(2026, 10, 4), "Gasto");
        await SembrarAporteAsync("Octubre", 50m, new DateTime(2026, 10, 10));
        await SembrarAporteAsync("Septiembre", 100m, new DateTime(2026, 9, 10));

        var dto = await ObtenerResumenAsync(_controller, anio: 2026, mes: 10);

        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(400m, dto.TotalGastos);
        Assert.Equal(50m, dto.TotalAhorro);
        Assert.Equal(550m, dto.Balance);
    }

    [Fact]
    public async Task GetResumen_RangoVacio_TieneDatosFalseYMontosEnCero()
    {
        var (ingreso, _, _) = await SembrarCategoriasAsync();
        await SembrarMovimientoAsync(ingreso.IdCategoria, 1000m, new DateTime(2026, 10, 5), "Ingreso");
        await SembrarAporteAsync("Vacío", 80m, new DateTime(2026, 10, 6));

        var dto = await ObtenerResumenAsync(_controller,
            fechaInicio: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            fechaFin: new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.False(dto.TieneDatos);
        Assert.Equal(0m, dto.TotalIngresos);
        Assert.Equal(0m, dto.TotalGastos);
        Assert.Equal(0m, dto.TotalAhorro);
        Assert.Equal(0m, dto.Balance);
        Assert.Equal(0m, dto.IngresosSalario);
        Assert.Empty(dto.AportesPorMeta);
    }

    [Fact]
    public async Task GetResumen_IngresosSalario_SoloSumaLaCategoriaSalario()
    {
        var (ingreso, _, _) = await SembrarCategoriasAsync();
        var negocio = new Categoria { IdHogar = 1, Nombre = "Negocio", Tipo = "Ingreso" };
        _db.Categorias.Add(negocio);
        await _db.SaveChangesAsync();

        await SembrarMovimientoAsync(ingreso.IdCategoria, 600m, new DateTime(2026, 10, 5), "Ingreso");
        await SembrarMovimientoAsync(negocio.IdCategoria, 250m, new DateTime(2026, 10, 8), "Ingreso");

        var dto = await ObtenerResumenAsync(_controller, anio: 2026);

        Assert.Equal(850m, dto.TotalIngresos);
        Assert.Equal(600m, dto.IngresosSalario);
    }

    [Fact]
    public async Task GetResumen_AportesPorMeta_AgrupaYSoloIncluyeLosDelRango()
    {
        var (metaViaje, _) = await SembrarAporteAsync("Viaje", 100m, new DateTime(2026, 10, 2));
        var (metaAuto, _) = await SembrarAporteAsync("Auto", 300m, new DateTime(2026, 10, 5));
        // Segundo aporte a la MISMA meta Viaje: debe acumularse en el mismo grupo.
        _db.Aportes.Add(new AporteMeta { IdMeta = metaViaje.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = 50m, Fecha = new DateTime(2026, 10, 20) });
        // Fuera del rango: no debe aparecer.
        await SembrarAporteAsync("Casa", 999m, new DateTime(2026, 8, 1));
        await _db.SaveChangesAsync();

        var dto = await ObtenerResumenAsync(_controller,
            fechaInicio: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            fechaFin: new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(2, dto.AportesPorMeta.Count);
        Assert.Equal(metaAuto.IdMeta, dto.AportesPorMeta[0].IdMeta);
        Assert.Equal("Auto", dto.AportesPorMeta[0].Titulo);
        Assert.Equal(300m, dto.AportesPorMeta[0].Monto);
        Assert.Equal(metaViaje.IdMeta, dto.AportesPorMeta[1].IdMeta);
        Assert.Equal("Viaje", dto.AportesPorMeta[1].Titulo);
        Assert.Equal(150m, dto.AportesPorMeta[1].Monto);
    }

    [Fact]
    public async Task GetResumen_AhorroUnificado_NoDuplicaElMovimientoDelAporte()
    {
        var (_, _, ahorro) = await SembrarCategoriasAsync();

        // Aporte con su movimiento generado por AportesController (marca "aporte:{id}").
        var (meta, aporte) = await SembrarAporteAsync("Emergencia", 150m, new DateTime(2026, 10, 6));
        await SembrarMovimientoAsync(ahorro.IdCategoria, 150m, new DateTime(2026, 10, 6), "Ahorro", $"aporte:{aporte.IdAporte}");

        // Movimiento de ahorro hecho a mano, sin la marca: sí suma.
        await SembrarMovimientoAsync(ahorro.IdCategoria, 75m, new DateTime(2026, 10, 15), "Ahorro");

        var dto = await ObtenerResumenAsync(_controller, anio: 2026);

        // El aporte cuenta una sola vez (150), no dos; el manual suma aparte.
        Assert.Equal(225m, dto.TotalAhorro);
        var unicaMeta = Assert.Single(dto.AportesPorMeta);
        Assert.Equal(meta.IdMeta, unicaMeta.IdMeta);
        Assert.Equal(150m, unicaMeta.Monto);
    }
}

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

public class MovimientosFiltroUsuarioTests : IDisposable
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
        public Task<bool> EnviarResultadoSolicitudUnionAsync(string a, string b, string c, bool d) => Task.FromResult(true);
        public Task<bool> EnviarResultadoInvitacionAsync(string a, string b, string c, bool d) => Task.FromResult(true);
    }

    public MovimientosFiltroUsuarioTests()
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
        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Alimentación", Tipo = "Gasto" });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1,
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = 1,
            Monto = 100m,
            Fecha = new DateTime(2026, 9, 15),
            Tipo = "Gasto",
            Descripcion = "Movimiento del miembro 1"
        });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 2,
            IdHogar = 1,
            IdUsuario = 2,
            IdCategoria = 1,
            Monto = 250m,
            Fecha = new DateTime(2026, 9, 16),
            Tipo = "Gasto",
            Descripcion = "Movimiento del miembro 2"
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMovimientos_ConFiltroUsuarioId_DevuelveSoloLosMovimientosDeEseMiembro()
    {
        await SembrarMovimientosAsync();

        var result = await _controller.GetMovimientos(
            categoriaId: null, tipo: null, fechaInicio: null, fechaFin: null, usuarioId: 2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var respuesta = Assert.IsType<PaginatedResponse<Movimiento>>(ok.Value);

        Assert.Equal(1, respuesta.Total);
        var movimiento = Assert.Single(respuesta.Items);
        Assert.Equal(2, movimiento.IdUsuario);
        Assert.Equal("Movimiento del miembro 2", movimiento.Descripcion);
    }

    [Fact]
    public async Task GetMovimientos_SinFiltroUsuarioId_DevuelveTodosLosDelHogar()
    {
        await SembrarMovimientosAsync();

        var result = await _controller.GetMovimientos(
            categoriaId: null, tipo: null, fechaInicio: null, fechaFin: null, usuarioId: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var respuesta = Assert.IsType<PaginatedResponse<Movimiento>>(ok.Value);

        Assert.Equal(2, respuesta.Total);
        // Ordenados por Fecha descendente: primero el movimiento más reciente (miembro 2).
        Assert.Equal(new[] { 2, 1 }, respuesta.Items.Select(m => m.IdUsuario));
    }
}

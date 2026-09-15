using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

public class MovimientosExportacionTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly MovimientosController _controller;

    public MovimientosExportacionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _controller = new MovimientosController(_db);

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

    [Fact]
    public async Task ExportarCsv_RetornaSoloMovimientosDelHogar()
    {
        var categoria = new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Alimentación", Tipo = "Gasto" };
        _db.Categorias.Add(categoria);
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1,
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = 1,
            Monto = 25.50m,
            Fecha = new DateTime(2026, 9, 15),
            Tipo = "Gasto",
            Descripcion = "Compra, semanal"
        });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 2,
            IdHogar = 2,
            IdUsuario = 2,
            IdCategoria = 1,
            Monto = 99m,
            Fecha = new DateTime(2026, 9, 14),
            Tipo = "Gasto"
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Exportar("csv");

        var file = Assert.IsType<FileContentResult>(result);
        var contenido = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Equal("text/csv", file.ContentType);
        Assert.Contains("\"Compra, semanal\"", contenido);
        Assert.DoesNotContain("99.00", contenido);
    }

    [Fact]
    public async Task ExportarJson_RetornaArchivoJson()
    {
        var result = await _controller.Exportar("json");

        var file = Assert.IsType<FileContentResult>(result);

        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("[]", System.Text.Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task Exportar_FormatoInvalido_RetornaBadRequest()
    {
        var result = await _controller.Exportar("xml");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
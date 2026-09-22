using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

public class AportesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly AportesController _controller;

    public AportesControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _controller = new AportesController(_db);

        var claims = new List<Claim>
        {
            new("idUsuario", "1"),
            new("idHogar", "1")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    public void Dispose() => _db.Dispose();

    private async Task<MetaAhorro> CrearMeta(string titulo = "Meta test", decimal montoObjetivo = 1000)
    {
        var meta = new MetaAhorro
        {
            IdHogar = 1,
            Titulo = titulo,
            MontoObjetivo = montoObjetivo,
            MontoActual = 0,
            FechaLimite = DateTime.UtcNow.AddDays(30),
            Estado = "En progreso"
        };
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();
        return meta;
    }

    [Fact]
    public async Task GetAportes_MetaNoExiste_RetornaBadRequest()
    {
        var result = await _controller.GetAportes(999);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("no existe", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task GetAportes_MetaExiste_RetornaLista()
    {
        var meta = await CrearMeta();
        _db.Aportes.Add(new AporteMeta { IdMeta = meta.IdMeta, Monto = 100, Fecha = DateTime.UtcNow });
        _db.Aportes.Add(new AporteMeta { IdMeta = meta.IdMeta, Monto = 200, Fecha = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAportes(meta.IdMeta);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var aportes = Assert.IsAssignableFrom<IEnumerable<AporteMeta>>(okResult.Value);
        Assert.Equal(2, aportes.Count());
    }

    [Fact]
    public async Task Create_AgregaAporteYActualizaMeta()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var aporte = new AporteMeta { IdMeta = meta.IdMeta, Monto = 250, Fecha = DateTime.UtcNow };

        var result = await _controller.Create(aporte);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var aporteCreado = Assert.IsType<AporteMeta>(createdResult.Value);
        Assert.Equal(250, aporteCreado.Monto);

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal(250, metaActualizada!.MontoActual);
        Assert.Equal("En progreso", metaActualizada.Estado);
    }

    [Fact]
    public async Task Create_MontoSuperaObjetivo_MarcaComoCompletada()
    {
        var meta = await CrearMeta(montoObjetivo: 500);
        var aporte = new AporteMeta { IdMeta = meta.IdMeta, Monto = 600, Fecha = DateTime.UtcNow };

        await _controller.Create(aporte);

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal("Completada", metaActualizada!.Estado);
    }

    [Fact]
    public async Task Create_MetaNoExiste_RetornaBadRequest()
    {
        var aporte = new AporteMeta { IdMeta = 999, Monto = 100, Fecha = DateTime.UtcNow };
        var result = await _controller.Create(aporte);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("no existe", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task Delete_EliminaAporteYRestaMonto()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var aporte = new AporteMeta { IdMeta = meta.IdMeta, Monto = 300, Fecha = DateTime.UtcNow };
        _db.Aportes.Add(aporte);
        meta.MontoActual = 300;
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(aporte.IdAporte);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Aportes.FindAsync(aporte.IdAporte));
        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal(0, metaActualizada!.MontoActual);
    }

    [Fact]
    public async Task Delete_AporteNoExiste_RetornaNotFound()
    {
        var result = await _controller.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_SumaMontosMultiples()
    {
        var meta = await CrearMeta(montoObjetivo: 5000);

        await _controller.Create(new AporteMeta { IdMeta = meta.IdMeta, Monto = 1000, Fecha = DateTime.UtcNow });
        await _controller.Create(new AporteMeta { IdMeta = meta.IdMeta, Monto = 500, Fecha = DateTime.UtcNow });
        await _controller.Create(new AporteMeta { IdMeta = meta.IdMeta, Monto = 200, Fecha = DateTime.UtcNow });

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal(1700, metaActualizada!.MontoActual);
    }
}

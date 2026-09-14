using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

public class MetasAhorroControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly MetasAhorroController _controller;

    public MetasAhorroControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _controller = new MetasAhorroController(_db);

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

    [Fact]
    public async Task GetMetas_RetornaListaVacia()
    {
        var result = await _controller.GetMetas();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metas = Assert.IsAssignableFrom<IEnumerable<MetaAhorro>>(okResult.Value);
        Assert.Empty(metas);
    }

    [Fact]
    public async Task GetMetas_RetornaSoloMetasDelHogar()
    {
        _db.MetasAhorro.Add(new MetaAhorro { IdMeta = 1, IdHogar = 1, Titulo = "Meta 1", MontoObjetivo = 1000, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso" });
        _db.MetasAhorro.Add(new MetaAhorro { IdMeta = 2, IdHogar = 2, Titulo = "Meta 2", MontoObjetivo = 2000, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetMetas();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metas = Assert.IsAssignableFrom<IEnumerable<MetaAhorro>>(okResult.Value);
        Assert.Single(metas);
    }

    [Fact]
    public async Task GetMeta_RetornaMetaPorId()
    {
        var meta = new MetaAhorro { IdMeta = 1, IdHogar = 1, Titulo = "Vacaciones", MontoObjetivo = 5000, MontoActual = 1000, FechaLimite = DateTime.UtcNow.AddDays(90), Estado = "En progreso" };
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();

        var result = await _controller.GetMeta(1);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var metaResult = Assert.IsType<MetaAhorro>(okResult.Value);
        Assert.Equal("Vacaciones", metaResult.Titulo);
        Assert.Equal(5000, metaResult.MontoObjetivo);
    }

    [Fact]
    public async Task GetMeta_NoExiste_RetornaNotFound()
    {
        var result = await _controller.GetMeta(999);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_AgregaMetaConValoresDefault()
    {
        var nuevaMeta = new MetaAhorro { Titulo = "Nuevo auto", MontoObjetivo = 15000, FechaLimite = DateTime.UtcNow.AddYears(1) };

        var result = await _controller.Create(nuevaMeta);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var metaCreada = Assert.IsType<MetaAhorro>(createdResult.Value);
        Assert.Equal("Nuevo auto", metaCreada.Titulo);
        Assert.Equal(0, metaCreada.MontoActual);
        Assert.Equal("En progreso", metaCreada.Estado);
        Assert.Equal(1, metaCreada.IdHogar);
    }

    [Fact]
    public async Task Update_ModificaCampos()
    {
        var meta = new MetaAhorro { IdMeta = 1, IdHogar = 1, Titulo = "Original", MontoObjetivo = 1000, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(30), Estado = "En progreso" };
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();

        var input = new MetaAhorro { Titulo = "Modificado", MontoObjetivo = 2000, FechaLimite = DateTime.UtcNow.AddDays(60) };
        var result = await _controller.Update(1, input);

        Assert.IsType<NoContentResult>(result);
        var metaActualizada = await _db.MetasAhorro.FindAsync(1);
        Assert.Equal("Modificado", metaActualizada!.Titulo);
        Assert.Equal(2000, metaActualizada.MontoObjetivo);
    }

    [Fact]
    public async Task Update_NoExiste_RetornaNotFound()
    {
        var input = new MetaAhorro { Titulo = "Test", MontoObjetivo = 1000, FechaLimite = DateTime.UtcNow };
        var result = await _controller.Update(999, input);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_EliminaMeta()
    {
        var meta = new MetaAhorro { IdMeta = 1, IdHogar = 1, Titulo = "Para borrar", MontoObjetivo = 500, MontoActual = 0, FechaLimite = DateTime.UtcNow.AddDays(10), Estado = "En progreso" };
        _db.MetasAhorro.Add(meta);
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.MetasAhorro.FindAsync(1));
    }

    [Fact]
    public async Task Delete_NoExiste_RetornaNotFound()
    {
        var result = await _controller.Delete(999);
        Assert.IsType<NotFoundResult>(result);
    }
}

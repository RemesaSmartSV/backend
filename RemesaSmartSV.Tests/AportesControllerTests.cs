using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
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

        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia test" });
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            IdHogar = 1,
            Nombre = "Usuario test",
            Correo = "aportes.test@gmail.com",
            ContrasenaHash = "hash",
            Rol = "Miembro",
            FechaRegistro = DateTime.UtcNow,
        });
        _db.SaveChanges();

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
        _db.Aportes.Add(new AporteMeta { IdMeta = meta.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = 100, Fecha = DateTime.UtcNow });
        _db.Aportes.Add(new AporteMeta { IdMeta = meta.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = 200, Fecha = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAportes(meta.IdMeta);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var aportes = Assert.IsAssignableFrom<IEnumerable<AporteResponseDto>>(okResult.Value);
        Assert.Equal(2, aportes.Count());
    }

    [Fact]
    public async Task Create_AgregaAporteYActualizaMeta()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var request = new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 250, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() };

        var result = await _controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var aporteCreado = Assert.IsType<AporteResponseDto>(createdResult.Value);
        Assert.Equal(250, aporteCreado.Monto);

        var aporteDb = await _db.Aportes.SingleAsync(a => a.IdAporte == aporteCreado.IdAporte);
        Assert.Equal(1, aporteDb.IdHogar);
        Assert.Equal(1, aporteDb.IdUsuario);

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal(250, metaActualizada!.MontoActual);
        Assert.Equal("En progreso", metaActualizada.Estado);
    }

    [Fact]
    public async Task Create_MontoSuperaObjetivo_MarcaComoCompletada()
    {
        var meta = await CrearMeta(montoObjetivo: 500);
        var aporte = new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 600, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() };

        await _controller.Create(aporte);

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal("Completada", metaActualizada!.Estado);
    }

    [Fact]
    public async Task Create_MetaNoExiste_RetornaBadRequest()
    {
        var aporte = new CrearAporteRequest { IdMeta = 999, Monto = 100, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() };
        var result = await _controller.Create(aporte);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("no existe", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task Delete_EliminaAporteYRestaMonto()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var aporte = new AporteMeta { IdMeta = meta.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = 300, Fecha = DateTime.UtcNow };
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

        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 1000, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() });
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 500, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() });
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 200, Fecha = DateTime.UtcNow, IdempotencyKey = Guid.NewGuid() });

        var metaActualizada = await _db.MetasAhorro.FindAsync(meta.IdMeta);
        Assert.Equal(1700, metaActualizada!.MontoActual);
    }

    [Fact]
    public async Task Create_ReintentoConMismaClave_NoDuplicaAporteNiSaldo()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var request = new CrearAporteRequest
        {
            IdMeta = meta.IdMeta,
            Monto = 125,
            Fecha = DateTime.UtcNow,
            Notas = "aporte de prueba",
            IdempotencyKey = Guid.NewGuid(),
        };

        var primero = await _controller.Create(request);
        var reintento = await _controller.Create(request);

        Assert.IsType<CreatedAtActionResult>(primero.Result);
        Assert.IsType<OkObjectResult>(reintento.Result);
        Assert.Equal(1, await _db.Aportes.CountAsync(a => a.IdMeta == meta.IdMeta));
        Assert.Equal(125, (await _db.MetasAhorro.FindAsync(meta.IdMeta))!.MontoActual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_MontoNoPositivo_Rechaza(decimal monto)
    {
        var meta = await CrearMeta();
        var result = await _controller.Create(new CrearAporteRequest
        {
            IdMeta = meta.IdMeta,
            Monto = monto,
            IdempotencyKey = Guid.NewGuid(),
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await _db.Aportes.ToListAsync());
    }

    // =====================================================================
    // Movimiento de ahorro generado por el aporte (ahorro-en-dashboard)
    // =====================================================================

    [Fact]
    public async Task Create_GeneraMovimientoDeAhorroConElUsuarioQueAporta()
    {
        var meta = await CrearMeta(titulo: "Viaje familiar");
        var fecha = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var request = new CrearAporteRequest
        {
            IdMeta = meta.IdMeta,
            Monto = 150,
            Fecha = fecha,
            IdempotencyKey = Guid.NewGuid(),
        };

        var result = await _controller.Create(request);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var aporteCreado = Assert.IsType<AporteResponseDto>(created.Value);

        var movimiento = Assert.Single(await _db.Movimientos.ToListAsync());
        Assert.Equal("Ahorro", movimiento.Tipo);
        Assert.Equal(1, movimiento.IdUsuario);
        Assert.Equal(1, movimiento.IdHogar);
        Assert.Equal(150, movimiento.Monto);
        Assert.Equal(fecha, movimiento.Fecha);
        Assert.Equal($"aporte:{aporteCreado.IdAporte}", movimiento.OrigenEmisora);
        Assert.Contains("Viaje familiar", movimiento.Descripcion);

        // La categoría del movimiento es de tipo Ahorro y pertenece al hogar.
        var categoria = await _db.Categorias.SingleAsync(c => c.IdCategoria == movimiento.IdCategoria);
        Assert.Equal("Ahorro", categoria.Tipo);
        Assert.Equal(1, categoria.IdHogar);
        Assert.Equal("Otros ahorros", categoria.Nombre);
    }

    [Fact]
    public async Task Create_ReutilizaLaMismaCategoriaDeAhorroEntreAportes()
    {
        var meta = await CrearMeta(montoObjetivo: 5000);

        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 100, IdempotencyKey = Guid.NewGuid() });
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 200, IdempotencyKey = Guid.NewGuid() });

        var movimientos = await _db.Movimientos.OrderBy(m => m.IdMovimiento).ToListAsync();
        Assert.Equal(2, movimientos.Count);
        Assert.Equal(movimientos[0].IdCategoria, movimientos[1].IdCategoria);

        // No se crea una categoría por aporte: sigue habiendo una sola de ahorro.
        Assert.Equal(1, await _db.Categorias.CountAsync(c => c.IdHogar == 1 && c.Tipo == "Ahorro"));
    }

    [Fact]
    public async Task Create_CategoriaDeAhorroExistenteDelHogar_SeReutilizaSinCrearOtra()
    {
        // El hogar ya tiene su propia "Otros ahorros" (creada antes del aporte).
        var preexistente = new Categoria { IdHogar = 1, Nombre = "Otros ahorros", Tipo = "Ahorro" };
        _db.Categorias.Add(preexistente);
        await _db.SaveChangesAsync();

        var meta = await CrearMeta();
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 50, IdempotencyKey = Guid.NewGuid() });

        var movimiento = Assert.Single(await _db.Movimientos.ToListAsync());
        Assert.Equal(preexistente.IdCategoria, movimiento.IdCategoria);
        Assert.Equal(1, await _db.Categorias.CountAsync(c => c.IdHogar == 1 && c.Tipo == "Ahorro"));
    }

    [Fact]
    public async Task Create_HogarSinCategoriaDeAhorro_LaCreaDeFormaIdempotente()
    {
        var meta = await CrearMeta();

        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 75, IdempotencyKey = Guid.NewGuid() });
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 25, IdempotencyKey = Guid.NewGuid() });

        var categoriasAhorro = await _db.Categorias.Where(c => c.IdHogar == 1 && c.Tipo == "Ahorro").ToListAsync();
        var categoria = Assert.Single(categoriasAhorro);
        Assert.Equal("Otros ahorros", categoria.Nombre);
        Assert.Equal(2, await _db.Movimientos.CountAsync());
    }

    [Fact]
    public async Task Create_ReintentoConMismaClave_NoDuplicaMovimientos()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var request = new CrearAporteRequest
        {
            IdMeta = meta.IdMeta,
            Monto = 125,
            Fecha = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid(),
        };

        var primero = await _controller.Create(request);
        var reintento = await _controller.Create(request);

        Assert.IsType<CreatedAtActionResult>(primero.Result);
        Assert.IsType<OkObjectResult>(reintento.Result);
        Assert.Equal(1, await _db.Aportes.CountAsync());
        // El reintento no vuelve a generar el movimiento: sigue habiendo uno solo.
        Assert.Equal(1, await _db.Movimientos.CountAsync(m => m.Tipo == "Ahorro"));
    }

    [Fact]
    public async Task Delete_EliminaElMovimientoGeneradoPorElAporte()
    {
        var meta = await CrearMeta(montoObjetivo: 1000);
        var creado = await _controller.Create(new CrearAporteRequest
        {
            IdMeta = meta.IdMeta,
            Monto = 300,
            IdempotencyKey = Guid.NewGuid(),
        });
        var aporte = Assert.IsType<CreatedAtActionResult>(creado.Result);
        var aporteCreado = Assert.IsType<AporteResponseDto>(aporte.Value);

        Assert.Single(await _db.Movimientos.ToListAsync());

        var result = await _controller.Delete(aporteCreado.IdAporte);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Aportes.FindAsync(aporteCreado.IdAporte));
        Assert.Empty(await _db.Movimientos.ToListAsync());
    }

    [Fact]
    public async Task Delete_AporteSinMovimiento_NoEliminaMovimientosAjenos()
    {
        // Aporte anterior a la feature: existe pero nunca generó movimiento.
        var meta = await CrearMeta(montoObjetivo: 1000);
        var aporte = new AporteMeta { IdMeta = meta.IdMeta, IdHogar = 1, IdUsuario = 1, Monto = 300, Fecha = DateTime.UtcNow };
        _db.Aportes.Add(aporte);
        meta.MontoActual = 300;

        // Un movimiento de ahorro que no pertenece a ese aporte.
        _db.Categorias.Add(new Categoria { IdHogar = 1, Nombre = "Otros ahorros", Tipo = "Ahorro" });
        await _db.SaveChangesAsync();
        var categoria = await _db.Categorias.SingleAsync(c => c.IdHogar == 1 && c.Tipo == "Ahorro");
        _db.Movimientos.Add(new Movimiento
        {
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = categoria.IdCategoria,
            Monto = 999,
            Fecha = DateTime.UtcNow,
            Tipo = "Ahorro",
            OrigenEmisora = "aporte:999999",
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(aporte.IdAporte);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Aportes.FindAsync(aporte.IdAporte));
        Assert.Single(await _db.Movimientos.ToListAsync());
    }

    [Fact]
    public async Task Delete_ElMovimientoDeUnAporteNoTocaElDeOtro()
    {
        var meta = await CrearMeta(montoObjetivo: 5000);

        var primero = await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 100, IdempotencyKey = Guid.NewGuid() });
        await _controller.Create(new CrearAporteRequest { IdMeta = meta.IdMeta, Monto = 200, IdempotencyKey = Guid.NewGuid() });

        var aporteBorrar = Assert.IsType<CreatedAtActionResult>(primero.Result);
        var aporteDto = Assert.IsType<AporteResponseDto>(aporteBorrar.Value);

        await _controller.Delete(aporteDto.IdAporte);

        var restante = Assert.Single(await _db.Movimientos.ToListAsync());
        Assert.Equal(200, restante.Monto);
        Assert.Equal($"aporte:{(await _db.Aportes.SingleAsync()).IdAporte}", restante.OrigenEmisora);
    }
}

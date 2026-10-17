using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas de duplicados en <see cref="PresupuestosController"/> (T12): solo puede
/// existir un presupuesto por hogar, categoría y mes.
/// OJO: EF InMemory no aplica índices únicos ni filtros, así que estos tests cubren
/// el chequeo previo del controller; el índice unique se valida en PostgreSQL.
/// </summary>
public class PresupuestosDuplicadosTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public PresupuestosDuplicadosTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private PresupuestosController CrearController(int idHogar)
    {
        var controller = new PresupuestosController(_db);
        var claims = new List<Claim> { new("idHogar", idHogar.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private async Task SembrarCategorias(int idHogar, params int[] idsCategorias)
    {
        foreach (var id in idsCategorias)
            _db.Categorias.Add(new Categoria
            {
                IdCategoria = id,
                IdHogar = idHogar,
                Nombre = $"Categoria {id}",
                Tipo = "Gasto"
            });
        await _db.SaveChangesAsync();
    }

    private static Presupuesto Nuevo(int idCategoria, DateTime mes) => new()
    {
        IdCategoria = idCategoria,
        MontoLimite = 350.00m,
        MesAnio = mes
    };

    private static readonly DateTime Octubre = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Noviembre = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_MismaCategoriaYMes_SegundoRetornaConflictYQuedaUnRegistro()
    {
        await SembrarCategorias(1, 1);
        var controller = CrearController(idHogar: 1);

        var primero = await controller.Create(Nuevo(idCategoria: 1, Octubre));
        Assert.IsType<CreatedAtActionResult>(primero.Result);

        var segundo = await controller.Create(Nuevo(idCategoria: 1, Octubre));

        var conflict = Assert.IsType<ConflictObjectResult>(segundo.Result);
        Assert.Contains("Ya existe un presupuesto", conflict.Value?.ToString());

        // El doble submit no dejó dos registros: quedó exactamente uno.
        Assert.Equal(1, await _db.Presupuestos.CountAsync());
    }

    [Fact]
    public async Task Create_MismaCategoriaEnMesesDistintos_AmbosSeCrean()
    {
        await SembrarCategorias(1, 1);
        var controller = CrearController(idHogar: 1);

        var primero = await controller.Create(Nuevo(idCategoria: 1, Octubre));
        var segundo = await controller.Create(Nuevo(idCategoria: 1, Noviembre));

        Assert.IsType<CreatedAtActionResult>(primero.Result);
        Assert.IsType<CreatedAtActionResult>(segundo.Result);
        Assert.Equal(2, await _db.Presupuestos.CountAsync());
    }

    [Fact]
    public async Task Create_CategoriasDistintasEnElMismoMes_AmbosSeCrean()
    {
        await SembrarCategorias(1, 1, 2);
        var controller = CrearController(idHogar: 1);

        var primero = await controller.Create(Nuevo(idCategoria: 1, Octubre));
        var segundo = await controller.Create(Nuevo(idCategoria: 2, Octubre));

        Assert.IsType<CreatedAtActionResult>(primero.Result);
        Assert.IsType<CreatedAtActionResult>(segundo.Result);
        Assert.Equal(2, await _db.Presupuestos.CountAsync());
    }

    [Fact]
    public async Task Update_MoverAUnaClaveYaOcupada_RetornaConflict()
    {
        await SembrarCategorias(1, 1, 2);
        var controller = CrearController(idHogar: 1);

        // Dos presupuestos válidos: (cat 1, octubre) y (cat 2, noviembre).
        await controller.Create(Nuevo(idCategoria: 1, Octubre));
        var otro = await controller.Create(Nuevo(idCategoria: 2, Noviembre));
        var idOtro = Assert.IsType<CreatedAtActionResult>(otro.Result);
        var idOtroPresupuesto = ((Presupuesto)idOtro.Value!).IdPresupuesto;

        // Mover el segundo a la clave ocupada por el primero debe chocar.
        var resultado = await controller.Update(idOtroPresupuesto, Nuevo(idCategoria: 1, Octubre));

        var conflict = Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Contains("Ya existe un presupuesto", conflict.Value?.ToString());
        Assert.Equal(2, await _db.Presupuestos.CountAsync());
    }

    [Fact]
    public async Task Update_SinCambiarClaveNoDisparaElChequeoDeDuplicado()
    {
        await SembrarCategorias(1, 1);
        var controller = CrearController(idHogar: 1);

        var creado = await controller.Create(Nuevo(idCategoria: 1, Octubre));
        var id = ((Presupuesto)Assert.IsType<CreatedAtActionResult>(creado.Result).Value!).IdPresupuesto;

        // Actualizar solo el monto sobre la misma clave no debe dar 409.
        var input = Nuevo(idCategoria: 1, Octubre);
        input.MontoLimite = 500.00m;
        var resultado = await controller.Update(id, input);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(500.00m, await _db.Presupuestos.Select(p => p.MontoLimite).SingleAsync());
    }

    /// <summary>
    /// Verifica la configuración del índice unique (no su comportamiento en BD):
    /// EF InMemory construye el mismo modelo, pero no ejecuta los índices.
    /// </summary>
    [Fact]
    public void Modelo_DeclaraIndiceUniqueDePresupuestoPorHogarCategoriaYMes()
    {
        var entidad = _db.Model.FindEntityType(typeof(Presupuesto));
        Assert.NotNull(entidad);

        var indice = entidad!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "IdHogar", "IdCategoria", "MesAnio" }));

        Assert.NotNull(indice);
        Assert.True(indice!.IsUnique);
    }
}

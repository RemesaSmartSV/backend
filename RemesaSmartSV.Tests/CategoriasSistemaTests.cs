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
/// Cubre el Cambio 7: cada hogar nace con las categorías del sistema, sin duplicar
/// las que el usuario ya tenga ni siquiera cuando difieren solo en acentos.
/// </summary>
public class CategoriasSistemaTests : IDisposable
{
    private const int Hogar = 1;
    private readonly ApplicationDbContext _db;

    public CategoriasSistemaTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Predeterminadas_EsVeintitresSinNombresRepetidos()
    {
        Assert.Equal(23, CategoriasSistema.Predeterminadas.Length);
        var nombres = CategoriasSistema.Predeterminadas.Select(c => c.Nombre).ToList();
        Assert.Equal(nombres.Count, nombres.Select(CategoriasSistema.Normalizar).Distinct().Count());
        Assert.All(CategoriasSistema.Predeterminadas, c =>
            Assert.Contains(c.Tipo, new[] { "Ingreso", "Gasto", "Ahorro" }));
    }

    [Fact]
    public async Task SeedAsync_CreaLasCategoriasDelSistemaConEsSistema()
    {
        var creadas = await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(23, creadas);

        var categorias = await _db.Categorias.Where(c => c.IdHogar == Hogar).ToListAsync();
        Assert.Equal(23, categorias.Count);
        Assert.All(categorias, c => Assert.True(c.EsSistema));
        Assert.Equal(6, categorias.Count(c => c.Tipo == "Ingreso"));
        Assert.Equal(10, categorias.Count(c => c.Tipo == "Gasto"));
        Assert.Equal(7, categorias.Count(c => c.Tipo == "Ahorro"));
    }

    [Fact]
    public async Task SeedAsync_EsIdempotenteLaSegundaLlamadaNoCreaNada()
    {
        await CategoriasSistema.SeedAsync(_db, Hogar);

        var segunda = await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(0, segunda);
        Assert.Equal(23, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
    }

    [Fact]
    public async Task SeedAsync_NoDuplicaCuandoSoloDifiereEnAcentos()
    {
        // El hogar ya tiene "Alimentacion" sin tilde: no hay que crear encima "Alimentación".
        _db.Categorias.Add(new Categoria
        {
            IdHogar = Hogar,
            Nombre = "Alimentacion",
            Tipo = "Gasto",
            EsSistema = false
        });
        await _db.SaveChangesAsync();

        var creadas = await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(22, creadas);
        Assert.Single(await _db.Categorias
            .Where(c => c.IdHogar == Hogar && CategoriasSistema.Normalizar(c.Nombre) == "alimentacion")
            .ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_ConservaLasCategoriasPersonalizadasDelHogar()
    {
        _db.Categorias.Add(new Categoria
        {
            IdHogar = Hogar,
            Nombre = "Gatos del gato",
            Tipo = "Gasto",
            EsSistema = false
        });
        await _db.SaveChangesAsync();

        await CategoriasSistema.SeedAsync(_db, Hogar);

        var total = await _db.Categorias.CountAsync(c => c.IdHogar == Hogar);
        Assert.Equal(24, total);
        Assert.Equal(1, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar && !c.EsSistema));
    }

    [Fact]
    public async Task SeedAsync_NoTocaOtrosHogares()
    {
        await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(23, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
        Assert.Equal(0, await _db.Categorias.CountAsync(c => c.IdHogar == 2));
    }

    [Theory]
    [InlineData("Alimentación", "alimentacion")]
    [InlineData("OTROS INGRESOS", "otros ingresos")]
    [InlineData("  Salud  ", "salud")]
    [InlineData("Educación", "educacion")]
    [InlineData("Vivienda", "vivienda")]
    public void Normalizar_PiegaAcentosMayusculasYEspacios(string entrada, string esperada)
        => Assert.Equal(esperada, CategoriasSistema.Normalizar(entrada));

    private CategoriasController ControllerDeHogar(int idHogar)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", "1"),
            new("idHogar", idHogar.ToString())
        };
        var controller = new CategoriasController(_db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private static PaginatedResponse<Categoria> Pagina(ActionResult<PaginatedResponse<Categoria>> resultado)
    {
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        return Assert.IsType<PaginatedResponse<Categoria>>(ok.Value);
    }

    [Fact]
    public async Task GetCategorias_HogarSinCategorias_SembrarLasDelSistema()
    {
        var controller = ControllerDeHogar(Hogar);

        var resultado = await controller.GetCategorias();

        Assert.Equal(23, Pagina(resultado).Total);
        Assert.Equal(23, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
    }

    [Fact]
    public async Task GetCategorias_SinHogar_NoSiembraElHogarCero()
    {
        var controller = ControllerDeHogar(0);

        var resultado = await controller.GetCategorias();

        Assert.Equal(0, Pagina(resultado).Total);
        Assert.Equal(0, await _db.Categorias.CountAsync(c => c.IdHogar == 0));
    }

    [Fact]
    public async Task GetCategorias_HogarConCategorias_NoDuplicaLasDelSistema()
    {
        await CategoriasSistema.SeedAsync(_db, Hogar);
        var controller = ControllerDeHogar(Hogar);

        var resultado = await controller.GetCategorias();

        Assert.Equal(23, Pagina(resultado).Total);
        Assert.Equal(23, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
    }
}

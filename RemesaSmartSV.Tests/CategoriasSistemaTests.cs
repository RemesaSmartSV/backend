using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
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
    public void Predeterminadas_EsDieciseisSinNombresRepetidos()
    {
        Assert.Equal(16, CategoriasSistema.Predeterminadas.Length);
        var nombres = CategoriasSistema.Predeterminadas.Select(c => c.Nombre).ToList();
        Assert.Equal(nombres.Count, nombres.Select(CategoriasSistema.Normalizar).Distinct().Count());
        Assert.All(CategoriasSistema.Predeterminadas, c =>
            Assert.Contains(c.Tipo, new[] { "Ingreso", "Gasto" }));
    }

    [Fact]
    public async Task SeedAsync_CreaLasCategoriasDelSistemaConEsSistema()
    {
        var creadas = await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(16, creadas);

        var categorias = await _db.Categorias.Where(c => c.IdHogar == Hogar).ToListAsync();
        Assert.Equal(16, categorias.Count);
        Assert.All(categorias, c => Assert.True(c.EsSistema));
        Assert.Equal(6, categorias.Count(c => c.Tipo == "Ingreso"));
        Assert.Equal(10, categorias.Count(c => c.Tipo == "Gasto"));
    }

    [Fact]
    public async Task SeedAsync_EsIdempotenteLaSegundaLlamadaNoCreaNada()
    {
        await CategoriasSistema.SeedAsync(_db, Hogar);

        var segunda = await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(0, segunda);
        Assert.Equal(16, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
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

        Assert.Equal(15, creadas);
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
        Assert.Equal(17, total);
        Assert.Equal(1, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar && !c.EsSistema));
    }

    [Fact]
    public async Task SeedAsync_NoTocaOtrosHogares()
    {
        await CategoriasSistema.SeedAsync(_db, Hogar);

        Assert.Equal(16, await _db.Categorias.CountAsync(c => c.IdHogar == Hogar));
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
}

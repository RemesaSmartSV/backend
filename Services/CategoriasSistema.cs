using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Services;

/// <summary>
/// Categorías del sistema que se crean automáticamente al crear un hogar (Cambio 7).
/// Portadas desde Proyecto_Prueba (HouseholdService.SeedSystemCategoriesAsync) y adaptadas
/// al modelo local: IdHogar entero, Tipo "Ingreso"|"Gasto" y flag EsSistema.
/// Los iconos se escriben como escapes \uXXXX para mantener la fuente libre de emojis.
/// </summary>
public static class CategoriasSistema
{
    /// <summary>(Nombre, Tipo, Icono) de las categorías predeterminadas del sistema.</summary>
    public static readonly (string Nombre, string Tipo, string Icono)[] Predeterminadas =
    {
        ("Remesas", "Ingreso", "\uD83C\uDF10"),
        ("Salario", "Ingreso", "\uD83D\uDCBC"),
        ("Negocio", "Ingreso", "\uD83C\uDFE2"),
        ("Freelance", "Ingreso", "\uD83D\uDCBB"),
        ("Inversión", "Ingreso", "\uD83D\uDCC8"),
        ("Otros ingresos", "Ingreso", "\uD83D\uDCB0"),
        ("Alimentación", "Gasto", "\uD83D\uDED2"),
        ("Vivienda", "Gasto", "\uD83C\uDFE0"),
        ("Servicios", "Gasto", "\uD83D\uDCA1"),
        ("Salud", "Gasto", "\uD83C\uDFE5"),
        ("Transporte", "Gasto", "\uD83D\uDE8C"),
        ("Educación", "Gasto", "\uD83D\uDCDA"),
        ("Ocio", "Gasto", "\uD83C\uDFAE"),
        ("Compras", "Gasto", "\uD83D\uDECD"),
        ("Deudas", "Gasto", "\uD83D\uDCB3"),
        ("Otros gastos", "Gasto", "\uD83E\uDDFE")
    };

    /// <summary>
    /// Siembra las categorías del sistema para un hogar, omitiendo las que ya existan con el
    /// mismo nombre. La comparación ignora mayúsculas y acentos, así "Alimentacion" no vuelve
    /// a crearse como "Alimentación". Usa el mismo plegado que la migración Fase2c, para que
    /// el código y el backfill no diverjan. Es idempotente: se puede llamar más de una vez.
    /// </summary>
    public static async Task<int> SeedAsync(ApplicationDbContext db, int idHogar, CancellationToken ct = default)
    {
        var existentes = await db.Categorias
            .Where(c => c.IdHogar == idHogar)
            .Select(c => c.Nombre)
            .ToListAsync(ct);

        var nuevas = Predeterminadas
            .Where(c => !existentes.Any(e => Normalizar(e) == Normalizar(c.Nombre)))
            .Select(c => new Categoria
            {
                IdHogar = idHogar,
                Nombre = c.Nombre,
                Tipo = c.Tipo,
                Icono = c.Icono,
                EsSistema = true
            })
            .ToList();

        if (nuevas.Count == 0) return 0;

        db.Categorias.AddRange(nuevas);
        await db.SaveChangesAsync(ct);
        return nuevas.Count;
    }

    /// <summary>
    /// Minúsculas, recortado y sin diacríticos. Equivalente SQL del plegado usado en la
    /// migración de backfill: translate(lower(...), 'áéíóúü', 'aeiouu').
    /// </summary>
    public static string Normalizar(string nombre)
    {
        const string conAcentos = "áéíóúü";
        const string sinAcentos = "aeiouu";

        var minus = nombre.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(minus.Length);
        foreach (var ch in minus)
        {
            var i = conAcentos.IndexOf(ch);
            sb.Append(i >= 0 ? sinAcentos[i] : ch);
        }

        return sb.ToString();
    }
}

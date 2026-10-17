using System.Globalization;
using System.Security.Cryptography;

namespace RemesaSmartSV.Entities;

/// <summary>
/// Genera el identificador compartible de una familia sin datos secuenciales.
/// El formato vigente es un ID de 6 dígitos numéricos (ej. "048293"). Los
/// códigos ya emitidos con el formato legacy "RS-FAM-" + 16 caracteres siguen
/// siendo válidos para buscar, de modo que los hogares existentes no quedan
/// inaccesibles.
/// </summary>
public static class CodigoHogar
{
    // Formato legacy: prefijo fijo + 16 caracteres. Se conservan las
    // constantes porque EsValido sigue reconociendo ese formato y los
    // controllers las referencian.
    public const string Prefix = "RS-FAM-";
    public const int Length = 23;

    private const int Digitos = 6;

    /// <summary>Espacio de búsqueda del formato vigente: 10^6 combinaciones.</summary>
    private const int EspacioCodigos = 1_000_000;

    /// <summary>Genera un código nuevo de 6 dígitos numéricos, sin prefijo (ej. "048293").</summary>
    public static string Generar()
    {
        // CultureInfo.InvariantCulture evita dígitos no-ASCII en culturas como
        // ar-EG, cuyo NumberFormatInfo.NativeDigits usaría cifras árabes.
        return RandomNumberGenerator.GetInt32(0, EspacioCodigos)
            .ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string Normalizar(string codigo)
        => codigo.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    /// <summary>Ambos formatos: el vigente (exactamente 6 dígitos) y el legacy ("RS-FAM-" + 16).</summary>
    public static bool EsValido(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        var normalized = Normalizar(codigo);

        // Formato vigente: exactamente 6 dígitos ASCII.
        if (normalized.Length == Digitos && normalized.All(char.IsAsciiDigit))
            return true;

        // Formato legacy: prefijo + 16 caracteres A-Z/0-9. El alfabeto es laxo
        // a propósito: la BD contiene códigos hex históricos (p. ej.
        // "RS-FAM-83656A6F88F54631") que nunca pasaron el alfabeto estricto del
        // generador anterior y deben seguir siendo buscables.
        return normalized.Length == Length
            && normalized.StartsWith(Prefix, StringComparison.Ordinal)
            && normalized[Prefix.Length..].All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
    }
}

using System.Security.Cryptography;

namespace RemesaSmartSV.Entities;

/// <summary>Genera el identificador compartible de una familia sin datos secuenciales.</summary>
public static class CodigoHogar
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const string Prefix = "RS-FAM-";
    public const int Length = 23;

    public static string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        Span<char> code = stackalloc char[16];
        for (var i = 0; i < code.Length; i++)
            code[i] = Alphabet[bytes[i] & 31];
        return Prefix + new string(code);
    }

    public static string Normalizar(string codigo)
        => codigo.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    public static bool EsValido(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        var normalized = Normalizar(codigo);
        return normalized.Length == Length
            && normalized.StartsWith(Prefix, StringComparison.Ordinal)
            && normalized[Prefix.Length..].All(Alphabet.Contains);
    }
}

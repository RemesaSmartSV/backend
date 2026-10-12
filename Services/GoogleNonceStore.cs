using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace RemesaSmartSV.Services;

/// <summary>Nonces temporales, de un solo uso, para vincular el callback GIS con esta sesión de login.</summary>
public sealed class GoogleNonceStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonces = new(StringComparer.Ordinal);

    public string Create()
    {
        PurgeExpired();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _nonces[nonce] = DateTimeOffset.UtcNow.Add(Lifetime);
        return nonce;
    }

    public bool TryConsume(string nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce) || !_nonces.TryRemove(nonce, out var expiresAt))
            return false;

        return expiresAt > DateTimeOffset.UtcNow;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _nonces)
        {
            if (item.Value <= now)
                _nonces.TryRemove(item.Key, out _);
        }
    }
}

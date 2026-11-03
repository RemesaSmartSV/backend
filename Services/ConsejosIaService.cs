using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RemesaSmartSV.Data;

namespace RemesaSmartSV.Services;

/// <summary>
/// Configuración del proveedor de consejos por IA (Groq, tier free).
/// Se construye una sola vez en Program.cs a partir de las variables de
/// entorno GROQ_API_KEY / GROQ_MODEL y se inyecta como singleton: el servicio
/// NUNCA lee el entorno directamente, así los tests pueden pasar una clave
/// explícita (o vacía) sin carreras contra variables de entorno.
/// </summary>
public sealed class ConsejosIaConfig
{
    /// <summary>Clave de console.groq.com. Vacía = IA desactivada (fallback a la DB).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo de Groq. Por defecto llama-3.3-70b-versatile.</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";
}

/// <summary>Consejo financiero devuelto por GET /api/ConsejosIa (JSON camelCase: {titulo, contenido}).</summary>
public sealed class ConsejoIaDto
{
    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("contenido")]
    public string Contenido { get; set; } = string.Empty;
}

public interface IConsejosIaService
{
    Task<IReadOnlyList<ConsejoIaDto>> ObtenerConsejosAsync(CancellationToken ct = default);
}

/// <summary>
/// Consejos financieros con IA gratuita (Groq) que rotan cada 2 horas:
/// 1. Caché en memoria (TTL absoluto 2 h) → si hay hit, se devuelve tal cual.
/// 2. Sin GROQ_API_KEY → fallback a la DB de inmediato (NO se cachea 2 h).
/// 3. Con clave → POST a Groq con respuesta en JSON; cualquier fallo
///    (timeout, no-2xx, JSON inválido, lista vacía) → log sin PII/claves →
///    fallback a la DB.
/// 4. Éxito → se cachea 2 h y se devuelve.
/// Fallback de DB: TipsFinancieros (EducacionFinanciera) ordenado por Titulo,
/// el mismo orden que GET /api/TipsFinancieros.
/// </summary>
public sealed class ConsejosIaService : IConsejosIaService
{
    internal const string CacheKey = "consejos_ia";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);

    private const string UrlGroq = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ConsejosIaConfig _config;
    private readonly ILogger<ConsejosIaService> _logger;

    public ConsejosIaService(
        HttpClient http,
        ApplicationDbContext db,
        IMemoryCache cache,
        ConsejosIaConfig config,
        ILogger<ConsejosIaService> logger)
    {
        _http = http;
        _db = db;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConsejoIaDto>> ObtenerConsejosAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ConsejoIaDto>? cacheados) && cacheados is not null)
            return cacheados;

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _logger.LogInformation("GROQ_API_KEY no configurada. Consejos desde la base de datos (IA desactivada).");
            return await ConsejosDeLaBaseAsync();
        }

        var consejos = await IntentarGroqAsync(ct);

        if (consejos is { Count: > 0 })
        {
            _cache.Set(CacheKey, consejos, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });
            return consejos;
        }

        return await ConsejosDeLaBaseAsync();
    }

    // -----------------------------------------------------------------
    // Llamada a Groq. Devuelve null ante cualquier fallo para que el
    // llamador caiga al fallback de la DB.
    // -----------------------------------------------------------------

    private async Task<IReadOnlyList<ConsejoIaDto>?> IntentarGroqAsync(CancellationToken ct)
    {
        try
        {
            using var request = ConstruirRequest();
            using var respuesta = await _http.SendAsync(request, ct);

            if (!respuesta.IsSuccessStatusCode)
            {
                // Solo el código HTTP: el body de Groq puede ecoear el prompt.
                _logger.LogWarning("Groq respondio {StatusCode}. Usando consejos de la base de datos.",
                    (int)respuesta.StatusCode);
                return null;
            }

            var json = await respuesta.Content.ReadAsStringAsync(ct);
            var consejos = ParsearConsejos(json);

            if (consejos is not { Count: > 0 })
            {
                _logger.LogWarning("Groq devolvio JSON sin consejos validos. Usando consejos de la base de datos.");
                return null;
            }

            _logger.LogInformation("Groq devolvio {Cantidad} consejos. Cacheados por 2 horas.", consejos.Count);
            return consejos;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelacion real del caller (no el timeout del HttpClient): propagar.
            throw;
        }
        catch (Exception ex)
        {
            // Sin mensaje de la excepcion en el log: puede contener la URI con
            // query o fragmentos del body. Tipo y punto bastan para diagnosticar.
            _logger.LogWarning("Groq fallo ({TipoExcepcion}). Usando consejos de la base de datos.",
                ex.GetType().Name);
            return null;
        }
    }

    private HttpRequestMessage ConstruirRequest()
    {
        var body = new
        {
            model = _config.Model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Eres un asesor financiero personal. Da consejos en español para usuarios de " +
                              "El Salvador, concretos y accionables, aplicables a presupuesto familiar, ahorro " +
                              "y remesas."
                },
                new
                {
                    role = "user",
                    content = "Dame exactamente 6 consejos financieros. Responde SOLO con JSON sin markdown " +
                              "y sin texto adicional, con este formato: " +
                              "{\"consejos\":[{\"titulo\":\"...\",\"contenido\":\"...\"}]}"
                }
            },
            // El modelo debe responder JSON puro: facilita el parseo defensivo.
            response_format = new { type = "json_object" },
            // Temperatura modesta: variedad suficiente entre rotaciones de 2 h
            // sin derivar en consejos inconsistentes.
            temperature = 0.7,
            max_tokens = 2048
        };

        var request = new HttpRequestMessage(HttpMethod.Post, UrlGroq)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        return request;
    }

    // -----------------------------------------------------------------
    // Parseo defensivo: content del chat → JSON {"consejos":[...]}.
    // Si el modelo envuelve el JSON en texto extra, se extrae la subcadena.
    // -----------------------------------------------------------------

    internal static IReadOnlyList<ConsejoIaDto>? ParsearConsejos(string jsonChatCompletion)
    {
        if (string.IsNullOrWhiteSpace(jsonChatCompletion))
            return null;

        string? contenido;
        try
        {
            using var doc = JsonDocument.Parse(jsonChatCompletion);
            contenido = doc.RootElement.TryGetProperty("choices", out var choices)
                        && choices.ValueKind == JsonValueKind.Array
                        && choices.GetArrayLength() > 0
                ? choices[0].TryGetProperty("message", out var mensaje)
                  && mensaje.ValueKind == JsonValueKind.Object
                  && mensaje.TryGetProperty("content", out var campo)
                  && campo.ValueKind == JsonValueKind.String
                    ? campo.GetString()
                    : null
                : jsonChatCompletion; // Acepta el JSON "pelado" si no trae envoltorio de chat.
        }
        catch (JsonException)
        {
            contenido = jsonChatCompletion;
        }

        if (string.IsNullOrWhiteSpace(contenido))
            return null;

        var objeto = ExtraerJsonObject(contenido);
        if (objeto is null)
            return null;

        try
        {
            var respuesta = JsonSerializer.Deserialize<RespuestaConsejos>(objeto,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var consejos = respuesta?.Consejos?
                .Where(c => c is not null
                            && !string.IsNullOrWhiteSpace(c.Titulo)
                            && !string.IsNullOrWhiteSpace(c.Contenido))
                .Select(c => new ConsejoIaDto
                {
                    Titulo = c.Titulo.Trim(),
                    Contenido = c.Contenido.Trim()
                })
                .ToList();

            return consejos is { Count: > 0 } ? consejos : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extrae el primer objeto JSON completo de un texto que puede tener
    /// preámbulo/póstexto ("Aquí tienes: {...} Espero que te sirva.").
    /// </summary>
    private static string? ExtraerJsonObject(string texto)
    {
        var inicio = texto.IndexOf('{');
        var fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio)
            return null;
        return texto[inicio..(fin + 1)];
    }

    // -----------------------------------------------------------------
    // Fallback a la DB (mismo orden que GET /api/TipsFinancieros: por Titulo).
    // -----------------------------------------------------------------

    private async Task<IReadOnlyList<ConsejoIaDto>> ConsejosDeLaBaseAsync()
        => await _db.TipsFinancieros
            .AsNoTracking()
            .OrderBy(t => t.Titulo)
            .Select(t => new ConsejoIaDto { Titulo = t.Titulo, Contenido = t.Contenido })
            .ToListAsync();

    // Modelos internos del payload de Groq (camelCase de la API OpenAI).
    private sealed class RespuestaConsejos
    {
        [JsonPropertyName("consejos")]
        public List<ConsejoIaDto>? Consejos { get; set; }
    }
}

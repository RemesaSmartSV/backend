using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// T3 — Consejos financieros por IA (Groq) con caché de 2 h y fallback a la DB.
/// Todas las pruebas usan un HttpMessageHandler stub: NUNCA hay red real.
/// </summary>
public class ConsejosIaTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ConsejosIaTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    // -----------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------

    /// <summary>Respuesta canónica de Groq, con el JSON envuelto en texto extra.</summary>
    private const string RespuestaGroqValida = """
    {
      "choices": [
        {
          "message": {
            "content": "Aquí tienes tus consejos:\n{\"consejos\":[{\"titulo\":\"Presupuesto familiar\",\"contenido\":\"Anota todos los gastos del hogar durante un mes.\"},{\"titulo\":\"Remesas con propósito\",\"contenido\":\"Destina un porcentaje fijo de cada remesa a ahorro.\"}]}\nEspero que te sirva."
          }
        }
      ]
    }
    """;

    private const string RespuestaGroqJsonInvalido = """
    {
      "choices": [
        { "message": { "content": "Lo siento, no pude generar JSON en este momento." } }
      ]
    }
    """;

    private const string RespuestaGroqListaVacia = """
    {
      "choices": [
        { "message": { "content": "{\"consejos\":[]}" } }
      ]
    }
    """;

    private sealed class StubGroqHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public int Llamadas { get; private set; }
        public Uri? UltimaUri { get; private set; }
        public AuthenticationHeaderValue? UltimaAutorizacion { get; private set; }
        public string? UltimoCuerpo { get; private set; }

        public StubGroqHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Llamadas++;
            UltimaUri = request.RequestUri;
            UltimaAutorizacion = request.Headers.Authorization;
            if (request.Content is not null)
                UltimoCuerpo = await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request);
        }
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo)
        => new(codigo)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };

    private ConsejosIaService CrearServicio(string apiKey, StubGroqHandler handler)
    {
        var http = new HttpClient(handler); // Sin Timeout real: el stub nunca duerme.
        return new ConsejosIaService(
            http,
            _db,
            _cache,
            new ConsejosIaConfig { ApiKey = apiKey, Model = "llama-3.3-70b-versatile" },
            NullLogger<ConsejosIaService>.Instance);
    }

    private async Task SembrarTipsAsync()
    {
        _db.TipsFinancieros.Add(new EducacionFinanciera
        {
            Titulo = "Ahorro",
            Contenido = "Guarda al menos el 10% de cada ingreso."
        });
        _db.TipsFinancieros.Add(new EducacionFinanciera
        {
            Titulo = "Presupuesto",
            Contenido = "Clasifica tus gastos antes de gastar."
        });
        await _db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------
    // 1. Sin clave → fallback a la DB, jamás se llama a Groq.
    // -----------------------------------------------------------------

    [Fact]
    public async Task SinApiKey_FallaAConsejosDeLaBaseDeDatos()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqValida));
        var servicio = CrearServicio(apiKey: string.Empty, handler);

        var consejos = await servicio.ObtenerConsejosAsync();

        Assert.Equal(0, handler.Llamadas); // Sin clave no se toca la red.
        Assert.Equal(2, consejos.Count);
        // Orden por Titulo (mismo criterio que GET /api/TipsFinancieros).
        Assert.Equal(new[] { "Ahorro", "Presupuesto" }, consejos.Select(c => c.Titulo));
        Assert.Contains("10%", consejos[0].Contenido);
    }

    // -----------------------------------------------------------------
    // 2. Groq OK → DTOs parseados (aunque vengan envueltos en texto) y
    //    cacheados: la segunda llamada no vuelve a pegar a Groq.
    // -----------------------------------------------------------------

    [Fact]
    public async Task GroqRespondeJsonValido_RetornaConsejosYCachea()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqValida));
        var servicio = CrearServicio("clave-de-prueba", handler);

        var primero = await servicio.ObtenerConsejosAsync();
        var segundo = await servicio.ObtenerConsejosAsync();

        // Contenido del modelo extraído pese al preámbulo/póstexto.
        Assert.Equal(2, primero.Count);
        Assert.Equal("Presupuesto familiar", primero[0].Titulo);
        Assert.Contains("Anota todos los gastos", primero[0].Contenido);
        // Vino de la IA, no de la DB.
        Assert.DoesNotContain(primero, c => c.Titulo == "Ahorro");

        // Segunda llamada: caché hit, handler sigue en 1.
        Assert.Equal(1, handler.Llamadas);
        Assert.Equal(primero.Select(c => c.Titulo), segundo.Select(c => c.Titulo));

        // Cableado de la petición: endpoint Groq, Bearer y cuerpo con el modelo.
        Assert.Equal("api.groq.com", handler.UltimaUri?.Host);
        Assert.Equal("Bearer", handler.UltimaAutorizacion?.Scheme);
        Assert.Equal("clave-de-prueba", handler.UltimaAutorizacion?.Parameter);
        Assert.Contains("llama-3.3-70b-versatile", handler.UltimoCuerpo);
    }

    // -----------------------------------------------------------------
    // 3a. Groq lanza excepción (red caída) → fallback a la DB.
    // -----------------------------------------------------------------

    [Fact]
    public async Task GroqFalla_FallaAConsejosDeLaBaseDeDatos()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => throw new HttpRequestException("sin red"));
        var servicio = CrearServicio("clave-de-prueba", handler);

        var consejos = await servicio.ObtenerConsejosAsync();

        Assert.Equal(1, handler.Llamadas);
        Assert.Equal(new[] { "Ahorro", "Presupuesto" }, consejos.Select(c => c.Titulo));
    }

    // -----------------------------------------------------------------
    // 3b. Groq responde 500 → fallback a la DB.
    // -----------------------------------------------------------------

    [Fact]
    public async Task GroqResponde500_FallaAConsejosDeLaBaseDeDatos()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.InternalServerError, "error"));
        var servicio = CrearServicio("clave-de-prueba", handler);

        var consejos = await servicio.ObtenerConsejosAsync();

        Assert.Equal(1, handler.Llamadas);
        Assert.Equal(new[] { "Ahorro", "Presupuesto" }, consejos.Select(c => c.Titulo));
    }

    // -----------------------------------------------------------------
    // 4a. Groq devuelve JSON sin objeto parseable → fallback a la DB.
    // -----------------------------------------------------------------

    [Fact]
    public async Task GroqRespondeJsonInvalido_FallaAConsejosDeLaBaseDeDatos()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqJsonInvalido));
        var servicio = CrearServicio("clave-de-prueba", handler);

        var consejos = await servicio.ObtenerConsejosAsync();

        Assert.Equal(1, handler.Llamadas);
        Assert.Equal(new[] { "Ahorro", "Presupuesto" }, consejos.Select(c => c.Titulo));
    }

    // -----------------------------------------------------------------
    // 4b. Groq devuelve {"consejos":[]} → fallback a la DB.
    // -----------------------------------------------------------------

    [Fact]
    public async Task GroqRespondeListaVacia_FallaAConsejosDeLaBaseDeDatos()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqListaVacia));
        var servicio = CrearServicio("clave-de-prueba", handler);

        var consejos = await servicio.ObtenerConsejosAsync();

        Assert.Equal(1, handler.Llamadas);
        Assert.Equal(new[] { "Ahorro", "Presupuesto" }, consejos.Select(c => c.Titulo));
    }

    // -----------------------------------------------------------------
    // 5. Humo del controlador: GET devuelve OkObjectResult con arreglo.
    // -----------------------------------------------------------------

    [Fact]
    public async Task ControllerGet_RetornaOkConArregloDeConsejos()
    {
        var fake = new ServicioFalso(new List<ConsejoIaDto>
        {
            new() { Titulo = "Tip de prueba", Contenido = "Contenido de prueba." }
        });
        var controller = new ConsejosIaController(fake);

        var resultado = await controller.Get(CancellationToken.None);

        // ActionResult<T> envuelve el OkObjectResult en .Result (convención del repo).
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var consejos = Assert.IsAssignableFrom<IEnumerable<ConsejoIaDto>>(ok.Value);
        Assert.Single(consejos);
        Assert.Equal("Tip de prueba", consejos.Single().Titulo);
    }

    private sealed class ServicioFalso : IConsejosIaService
    {
        private readonly IReadOnlyList<ConsejoIaDto> _consejos;
        public ServicioFalso(IReadOnlyList<ConsejoIaDto> consejos) => _consejos = consejos;
        public Task<IReadOnlyList<ConsejoIaDto>> ObtenerConsejosAsync(CancellationToken ct = default)
            => Task.FromResult(_consejos);
    }
}

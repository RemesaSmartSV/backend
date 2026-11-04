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

    // -----------------------------------------------------------------
    // 6. T2 — idioma: default "es", "en" válido, "fr" → 400, caché por
    //    idioma, prompt en el idioma y fallback de DB traducido.
    // -----------------------------------------------------------------

    [Fact]
    public async Task ControllerGet_SinIdioma_PasaEsPorDefecto()
    {
        var fake = new ServicioFalso(new List<ConsejoIaDto>
        {
            new() { Titulo = "Tip", Contenido = "Contenido." }
        });
        var controller = new ConsejosIaController(fake);

        var resultado = await controller.Get(CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado.Result);
        Assert.Equal("es", fake.UltimoIdioma);
    }

    [Fact]
    public async Task ControllerGet_IdiomaEn_PasaEnAlServicio()
    {
        var fake = new ServicioFalso(new List<ConsejoIaDto>
        {
            new() { Titulo = "Tip", Contenido = "Contenido." }
        });
        var controller = new ConsejosIaController(fake);

        var resultado = await controller.Get(CancellationToken.None, "en");

        Assert.IsType<OkObjectResult>(resultado.Result);
        Assert.Equal("en", fake.UltimoIdioma);
    }

    [Fact]
    public async Task ControllerGet_IdiomaInvalido_Retorna400()
    {
        var fake = new ServicioFalso(new List<ConsejoIaDto>());
        var controller = new ConsejosIaController(fake);

        var resultado = await controller.Get(CancellationToken.None, "fr");

        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Equal("El idioma debe ser es o en.", GetMensaje(badRequest));
        Assert.Null(fake.UltimoIdioma); // No llega al servicio.
    }

    /// <summary>Lee el { message = "..." } anónimo de los BadRequest del repo.</summary>
    private static string? GetMensaje(BadRequestObjectResult resultado)
    {
        var propiedad = resultado.Value?.GetType().GetProperty("message");
        return propiedad?.GetValue(resultado.Value) as string;
    }

    [Fact]
    public async Task SinApiKey_IdiomaEn_TraduceLosTipsSembrados()
    {
        // Los dos títulos/contenidos exactos de la migración Fase2d.
        _db.TipsFinancieros.Add(new EducacionFinanciera
        {
            Titulo = "Planificación de menú semanal",
            Contenido = "Elaborar un menú semanal antes de ir al supermercado te ayuda a comprar solo lo necesario y evitar desperdicios, reduciendo significativamente tus gastos mensuales."
        });
        _db.TipsFinancieros.Add(new EducacionFinanciera
        {
            Titulo = "Optimización del consumo eléctrico",
            Contenido = "Desconectar los electrodomésticos que no están en uso y aprovechar la luz natural puede disminuir hasta un 15% el monto de tu recibo de energía mensual."
        });
        await _db.SaveChangesAsync();

        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqValida));
        var servicio = CrearServicio(apiKey: string.Empty, handler);

        var en = await servicio.ObtenerConsejosAsync("en");
        var es = await servicio.ObtenerConsejosAsync();

        Assert.Equal(2, en.Count);
        Assert.Contains(en, c => c.Titulo == "Weekly meal planning");
        Assert.Contains(en, c => c.Titulo == "Optimizing electricity use");
        Assert.DoesNotContain(en, c => c.Titulo.Contains("Optimización"));

        // El camino en español sigue byte-idéntico al de siempre.
        Assert.Equal(2, es.Count);
        Assert.Contains(es, c => c.Titulo == "Planificación de menú semanal");
        Assert.Contains(es, c => c.Titulo == "Optimización del consumo eléctrico");
    }

    [Fact]
    public async Task Groq_IdiomaEn_CacheYPromptIndependientes()
    {
        await SembrarTipsAsync();
        var handler = new StubGroqHandler(_ => Respuesta(HttpStatusCode.OK, RespuestaGroqValida));
        var servicio = CrearServicio("clave-de-prueba", handler);

        await servicio.ObtenerConsejosAsync("es");
        var cuerpoEs = handler.UltimoCuerpo;
        await servicio.ObtenerConsejosAsync("en");
        var cuerpoEn = handler.UltimoCuerpo;

        // Prompt: solo cambia la instrucción de idioma. (System.Text.Json
        // escapa los acentos a \uXXXX, por eso se afirma sin la letra acentuada.)
        Assert.Contains("Da consejos en espa", cuerpoEs);
        Assert.Contains("Da consejos en ingl", cuerpoEn);

        // Caché por idioma: es → en son dos entradas distintas (2 llamadas),
        // y repetir el mismo idioma no vuelve a pegar a Groq (sigue en 2).
        Assert.Equal(2, handler.Llamadas);
        await servicio.ObtenerConsejosAsync("en");
        Assert.Equal(2, handler.Llamadas);
    }

    private sealed class ServicioFalso : IConsejosIaService
    {
        private readonly IReadOnlyList<ConsejoIaDto> _consejos;

        /// <summary>Último idioma recibido por el servicio (para assertions del controlador).</summary>
        public string? UltimoIdioma { get; private set; }

        public ServicioFalso(IReadOnlyList<ConsejoIaDto> consejos) => _consejos = consejos;

        public Task<IReadOnlyList<ConsejoIaDto>> ObtenerConsejosAsync(
            string idioma = "es", CancellationToken ct = default)
        {
            UltimoIdioma = idioma;
            return Task.FromResult(_consejos);
        }
    }
}

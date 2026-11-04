using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

/// <summary>
/// Consejos financieros: IA (Groq) con rotación de 2 horas y fallback a los
/// tips de la base de datos. Anónimo, igual que GET /api/TipsFinancieros.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConsejosIaController : ControllerBase
{
    private readonly IConsejosIaService _consejos;

    public ConsejosIaController(IConsejosIaService consejos) => _consejos = consejos;

    /// <summary>
    /// Obtiene la lista de consejos financieros (IA si hay clave GROQ_API_KEY,
    /// si no, tips de la DB). Devuelve un arreglo JSON crudo [{titulo, contenido}].
    /// </summary>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <param name="idioma">Idioma de los consejos: "es" (por defecto) u "en".</param>
    /// <response code="200">Devuelve el arreglo de consejos.</response>
    /// <response code="400">El idioma no es "es" ni "en".</response>
    [ProducesResponseType(typeof(IEnumerable<ConsejoIaDto>), StatusCodes.Status200OK)]
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ConsejoIaDto>>> Get(
        CancellationToken cancellationToken,
        [FromQuery] string idioma = "es")
    {
        // Vacío o ausente = español (compatibilidad con GET sin parámetro).
        idioma = string.IsNullOrWhiteSpace(idioma) ? "es" : idioma.Trim();

        if (idioma.Equals("es", StringComparison.OrdinalIgnoreCase))
            idioma = "es";
        else if (idioma.Equals("en", StringComparison.OrdinalIgnoreCase))
            idioma = "en";
        else
            return BadRequest(new { message = "El idioma debe ser es o en." });

        return Ok(await _consejos.ObtenerConsejosAsync(idioma, cancellationToken));
    }
}

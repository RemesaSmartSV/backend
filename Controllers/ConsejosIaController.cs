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
    /// <response code="200">Devuelve el arreglo de consejos.</response>
    [ProducesResponseType(typeof(IEnumerable<ConsejoIaDto>), StatusCodes.Status200OK)]
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ConsejoIaDto>>> Get(CancellationToken cancellationToken)
        => Ok(await _consejos.ObtenerConsejosAsync(cancellationToken));
}

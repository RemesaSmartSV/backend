using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

/// <summary>
/// Ledger de remesas del hogar autenticado: registro de envíos y recepciones
/// por canal, con montos, comisiones y resumen anual.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemesasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public RemesasController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Obtiene las remesas del hogar autenticado, ordenadas por fecha descendente, con filtros opcionales.</summary>
    /// <param name="anio">Filtra por el año de la fecha cuando se especifica.</param>
    /// <param name="direccion">Filtra por dirección: Envio o Recepcion.</param>
    /// <response code="200">Devuelve la lista de remesas del hogar que coincide con los filtros.</response>
    [ProducesResponseType(typeof(List<Remesa>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<List<Remesa>>> GetRemesas(
        [FromQuery] int? anio,
        [FromQuery] string? direccion)
    {
        var idHogar = User.GetIdHogar();
        var query = _db.Remesas.AsNoTracking().Where(r => r.IdHogar == idHogar);

        if (anio.HasValue)
            query = query.Where(r => r.Fecha.Year == anio.Value);
        if (!string.IsNullOrWhiteSpace(direccion))
            query = query.Where(r => r.Direccion == direccion);

        var remesas = await query.OrderByDescending(r => r.Fecha).ToListAsync();
        return Ok(remesas);
    }

    /// <summary>Crea una remesa para el hogar actual; cualquier miembro puede registrar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"direccion": "Envio", "canal": "WesternUnion", "montoEnviado": 100, "montoRecibido": 96, "comision": 4, "folio": "MTCN1234567890", "notas": "Envío a familia"}</c></remarks>
    /// <param name="input">Datos de la remesa que se registrará; el hogar y el usuario se toman del token.</param>
    /// <response code="201">La remesa fue creada.</response>
    /// <response code="400">La dirección o el canal no son válidos, el monto recibido supera el enviado o la remesa vinculada no existe en el hogar.</response>
    [ProducesResponseType(typeof(Remesa), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<Remesa>> Create([FromBody] Remesa input)
    {
        var error = await ValidarRemesaAsync(input, idRemesaExcluida: null);
        if (error is not null)
            return BadRequest(new { message = error });

        var idHogar = User.GetIdHogar();

        var remesa = new Remesa
        {
            IdHogar = idHogar,
            IdUsuario = User.GetIdUsuario(),
            Direccion = input.Direccion,
            Canal = input.Canal,
            MontoEnviado = input.MontoEnviado,
            MontoRecibido = input.MontoRecibido,
            Comision = input.Comision,
            Folio = input.Folio,
            Fecha = input.Fecha,
            Notas = input.Notas,
            IdRemesaVinculada = input.IdRemesaVinculada
        };

        _db.Remesas.Add(remesa);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRemesas), new { }, remesa);
    }

    /// <summary>Actualiza una remesa del hogar; solo el miembro que la registró o un Admin.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"direccion": "Recepcion", "canal": "Giros", "montoEnviado": 100, "montoRecibido": 97, "comision": 3, "folio": "GIRO-99", "notas": null}</c></remarks>
    /// <param name="id">Identificador de la remesa.</param>
    /// <param name="input">Nuevos datos de la remesa; el usuario que la registró no cambia.</param>
    /// <response code="204">La remesa fue actualizada.</response>
    /// <response code="400">La dirección o el canal no son válidos, el monto recibido supera el enviado o la remesa vinculada no existe en el hogar.</response>
    /// <response code="403">La remesa pertenece a tu hogar pero no la registraste y no sos Admin.</response>
    /// <response code="404">La remesa no existe o no pertenece a tu hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Remesa input)
    {
        var error = await ValidarRemesaAsync(input, idRemesaExcluida: id);
        if (error is not null)
            return BadRequest(new { message = error });

        var remesa = await _db.Remesas.FirstOrDefaultAsync(r => r.IdRemesa == id && r.IdHogar == User.GetIdHogar());
        if (remesa is null)
            return NotFound();

        if (remesa.IdUsuario != User.GetIdUsuario() && !User.IsInRole("Admin"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Solo el miembro que registró la remesa o un administrador puede modificarla." });

        remesa.Direccion = input.Direccion;
        remesa.Canal = input.Canal;
        remesa.MontoEnviado = input.MontoEnviado;
        remesa.MontoRecibido = input.MontoRecibido;
        remesa.Comision = input.Comision;
        remesa.Folio = input.Folio;
        remesa.Fecha = input.Fecha;
        remesa.Notas = input.Notas;
        remesa.IdRemesaVinculada = input.IdRemesaVinculada;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Elimina una remesa del hogar; solo el miembro que la registró o un Admin.</summary>
    /// <param name="id">Identificador de la remesa.</param>
    /// <response code="204">La remesa fue eliminada.</response>
    /// <response code="403">La remesa pertenece a tu hogar pero no la registraste y no sos Admin.</response>
    /// <response code="404">La remesa no existe o no pertenece a tu hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var remesa = await _db.Remesas.FirstOrDefaultAsync(r => r.IdRemesa == id && r.IdHogar == User.GetIdHogar());
        if (remesa is null)
            return NotFound();

        if (remesa.IdUsuario != User.GetIdUsuario() && !User.IsInRole("Admin"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Solo el miembro que registró la remesa o un administrador puede eliminarla." });

        _db.Remesas.Remove(remesa);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Obtiene el resumen anual de remesas del hogar, con totales y desglose por canal.</summary>
    /// <param name="anio">Año del resumen; por defecto el año actual en UTC.</param>
    /// <response code="200">Devuelve los totales del año y el desglose por canal.</response>
    [ProducesResponseType(typeof(ResumenRemesasDTO), StatusCodes.Status200OK)]
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenRemesasDTO>> GetResumen([FromQuery] int? anio)
    {
        var anioConsulta = anio ?? DateTime.UtcNow.Year;
        var idHogar = User.GetIdHogar();

        var remesas = await _db.Remesas
            .AsNoTracking()
            .Where(r => r.IdHogar == idHogar && r.Fecha.Year == anioConsulta)
            .ToListAsync();

        var porCanal = remesas
            .GroupBy(r => r.Canal)
            .Select(g => new ResumenRemesaCanalDTO(
                g.Key,
                g.Count(),
                g.Sum(r => r.MontoEnviado),
                g.Sum(r => r.MontoRecibido),
                g.Sum(r => r.Comision)))
            .OrderBy(c => c.Canal)
            .ToList();

        var resumen = new ResumenRemesasDTO(
            anioConsulta,
            remesas.Sum(r => r.MontoEnviado),
            remesas.Sum(r => r.MontoRecibido),
            remesas.Sum(r => r.Comision),
            remesas.Count,
            porCanal);

        return Ok(resumen);
    }

    /// <summary>
    /// Valida dirección, canal, montos y el vínculo con otra remesa del mismo hogar.
    /// Devuelve el mensaje de error para el 400, o <c>null</c> si todo es válido.
    /// </summary>
    private async Task<string?> ValidarRemesaAsync(Remesa input, int? idRemesaExcluida)
    {
        if (!EsDireccionValida(input.Direccion))
            return "La dirección debe ser Envio o Recepcion.";

        if (!EsCanalValido(input.Canal))
            return "El canal debe ser WesternUnion, Giros, AgenteBanco, Efectivo, BilleteraDigital u Otro.";

        if (input.MontoRecibido > input.MontoEnviado)
            return "El monto recibido no puede ser mayor que el monto enviado.";

        if (input.IdRemesaVinculada.HasValue)
        {
            if (input.IdRemesaVinculada.Value == idRemesaExcluida)
                return "La remesa no puede estar vinculada a sí misma.";

            var idHogar = User.GetIdHogar();
            var vinculada = await _db.Remesas
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdRemesa == input.IdRemesaVinculada.Value && r.IdHogar == idHogar);
            if (vinculada is null)
                return "La remesa vinculada no existe o no pertenece a tu hogar.";
        }

        return null;
    }

    private static bool EsDireccionValida(string? direccion)
        => string.Equals(direccion, RemesaConstantes.DireccionEnvio, StringComparison.Ordinal) ||
           string.Equals(direccion, RemesaConstantes.DireccionRecepcion, StringComparison.Ordinal);

    private static bool EsCanalValido(string? canal)
        => string.Equals(canal, RemesaConstantes.CanalWesternUnion, StringComparison.Ordinal) ||
           string.Equals(canal, RemesaConstantes.CanalGiros, StringComparison.Ordinal) ||
           string.Equals(canal, RemesaConstantes.CanalAgenteBanco, StringComparison.Ordinal) ||
           string.Equals(canal, RemesaConstantes.CanalEfectivo, StringComparison.Ordinal) ||
           string.Equals(canal, RemesaConstantes.CanalBilleteraDigital, StringComparison.Ordinal) ||
           string.Equals(canal, RemesaConstantes.CanalOtro, StringComparison.Ordinal);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HogaresController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuthService _authService;

    public HogaresController(ApplicationDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    /// <summary>Crea el hogar del usuario actual y lo asigna como Admin.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombreFamiliar": "Familia Rivera"}</c></remarks>
    /// <param name="request">Nombre familiar del hogar que se creará.</param>
    /// <response code="200">Hogar creado. Devuelve la sesión refrescada con el hogar y el rol.</response>
    /// <response code="404">El usuario autenticado no existe.</response>
    /// <response code="409">El usuario ya pertenece a un hogar.</response>
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<ActionResult<LoginResponse>> Create([FromBody] CrearHogarRequest request)
    {
        // GetIdHogar() devuelve 0 cuando el token no trae hogar asignado.
        if (User.GetIdHogar() != 0)
            return Conflict(new { message = "Ya pertenecés a un hogar." });

        var usuario = await _db.Usuarios.FindAsync(User.GetIdUsuario());
        if (usuario is null)
            return NotFound();
        // No confiar solo en el claim: puede estar obsoleto tras aceptar una solicitud.
        if (usuario.IdHogar is > 0)
            return Conflict(new { message = "Ya pertenecés a un hogar." });

        var hogar = new Hogar { NombreFamiliar = request.NombreFamiliar };
        var creado = false;
        for (var intento = 0; intento < 5 && !creado; intento++)
        {
            hogar.CodigoPublico = CodigoHogar.Generar();
            _db.Hogares.Add(hogar);
            try
            {
                await _db.SaveChangesAsync();
                creado = true;
            }
            catch (DbUpdateException ex) when (EsColisionCodigo(ex))
            {
                _db.Entry(hogar).State = EntityState.Detached;
                if (intento == 4) throw;
            }
        }

        // Cambio 7: todo hogar nace con las categorías del sistema.
        await CategoriasSistema.SeedAsync(_db, hogar.IdHogar);

        usuario.IdHogar = hogar.IdHogar;
        usuario.Rol = "Admin";
        await _db.SaveChangesAsync();

        // Se devuelve un LoginResponse refrescado: el token del request trae idHogar=0,
        // así que hace falta emitir un token nuevo que incluya el hogar y el rol.
        return Ok(_authService.BuildLoginResponse(usuario));
    }

    /// <summary>Busca una familia por su código público compartible; no concede acceso ni revela miembros.</summary>
    [HttpGet("buscar")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<HogarBusquedaDto>> BuscarPorCodigo([FromQuery] string codigo)
    {
        if (!CodigoHogar.EsValido(codigo))
            return BadRequest(new { message = "El código de familia no tiene un formato válido." });

        var normalizado = CodigoHogar.Normalizar(codigo);
        var hogar = await _db.Hogares.AsNoTracking()
            .Where(h => h.CodigoPublico == normalizado)
            .Select(h => new HogarBusquedaDto(h.CodigoPublico, h.NombreFamiliar))
            .FirstOrDefaultAsync();

        return hogar is null ? NotFound(new { message = "No se encontró una familia con ese código." }) : Ok(hogar);
    }

    /// <summary>Obtiene el hogar asociado al usuario actual.</summary>
    /// <response code="200">Devuelve el hogar del usuario.</response>
    /// <response code="404">No se encontró el hogar.</response>
    [ProducesResponseType(typeof(Hogar), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet]
    public async Task<ActionResult<Hogar>> GetMiHogar()
    {
        var hogar = await _db.Hogares.FindAsync(User.GetIdHogar());
        if (hogar is null)
            return NotFound();
        return Ok(hogar);
    }

    /// <summary>Actualiza el nombre familiar de un hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombreFamiliar": "Familia Rivera"}</c></remarks>
    /// <param name="id">Identificador del hogar.</param>
    /// <param name="request">Nuevo nombre familiar del hogar.</param>
    /// <response code="204">El hogar fue actualizado.</response>
    /// <response code="404">El hogar no existe o no pertenece al usuario actual.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHogarRequest request)
    {
        var hogar = await _db.Hogares.FindAsync(id);
        if (hogar is null || hogar.IdHogar != User.GetIdHogar())
            return NotFound();
        hogar.NombreFamiliar = request.NombreFamiliar;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Elimina el hogar del usuario actual.</summary>
    /// <param name="id">Identificador del hogar.</param>
    /// <response code="204">El hogar fue eliminado.</response>
    /// <response code="404">El hogar no existe o no pertenece al usuario actual.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var hogar = await _db.Hogares.FindAsync(id);
        if (hogar is null || hogar.IdHogar != User.GetIdHogar())
            return NotFound();
        _db.Hogares.Remove(hogar);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool EsColisionCodigo(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.UniqueViolation
           && postgres.ConstraintName == "IX_Hogares_CodigoPublico";
}

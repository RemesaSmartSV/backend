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
        // La autoridad sobre la membresía es la fila de la BD, no el claim idHogar
        // del token: ese claim se firma una sola vez y vive 8 h, así que sigue
        // diciendo "tenés hogar" aunque el usuario acabara de abandonar. Chequearlo
        // antes de leer la BD producía un 409 indebido con un token viejo.
        var usuario = await _db.Usuarios.FindAsync(User.GetIdUsuario());
        if (usuario is null)
            return NotFound();
        if (usuario.IdHogar is > 0)
            return Conflict(new { message = "Ya pertenecés a un hogar." });

        var hogar = new Hogar { NombreFamiliar = request.NombreFamiliar };
        var creado = false;
        // El código vigente tiene 10^6 combinaciones: la colisión con el índice
        // único IX_Hogares_CodigoPublico es improbable, pero se reintenta
        // generando otro código (máx. 5 intentos) antes de propagar el 409.
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

    /// <summary>Busca familias por nombre (parcial), sin distinguir mayúsculas ni acentos.</summary>
    /// <remarks>La comparación es por subcadena sobre el nombre familiar. Devuelve solo identificador
    /// y nombre; nunca el código público, requisito de privacidad: la búsqueda por nombre solo permite
    /// solicitar unión (que un Admin aprueba), no unirse directamente.</remarks>
    /// <param name="q">Texto de búsqueda; requerido, con al menos 3 caracteres.</param>
    /// <response code="200">Devuelve hasta 10 coincidencias ordenadas por relevancia (empieza-con antes que contiene, luego alfabético). Sin coincidencias devuelve lista vacía.</response>
    /// <response code="400">El texto de búsqueda falta o tiene menos de 3 caracteres.</response>
    [ProducesResponseType(typeof(IEnumerable<HogarResultadoBusquedaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet("buscar-por-nombre")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<IEnumerable<HogarResultadoBusquedaDto>>> BuscarPorNombre([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            return BadRequest(new { message = "La búsqueda debe tener al menos 3 caracteres." });

        // Mismo plegado que CategoriasSistema.Normalizar: minúsculas y sin acentos (áéíóúü).
        // La cadena ToLower + Replace va inline (y repetida en el OrderBy) porque EF necesita
        // verla en el árbol de expresiones para traducirla a LOWER/REPLACE en SQL.
        var consulta = CategoriasSistema.Normalizar(q);
        var resultados = await _db.Hogares.AsNoTracking()
            .Where(h => h.NombreFamiliar.ToLower()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u")
                .Contains(consulta))
            .OrderBy(h => h.NombreFamiliar.ToLower()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u")
                .StartsWith(consulta) ? 0 : 1)
            .ThenBy(h => h.NombreFamiliar)
            .Select(h => new HogarResultadoBusquedaDto(h.IdHogar, h.NombreFamiliar))
            .Take(10)
            .ToListAsync();

        return Ok(resultados);
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
    /// <remarks>
    /// Si la familia todavía tiene datos financieros (movimientos, metas de ahorro,
    /// aportes o presupuestos) no se elimina: esos datos se conservan por requisito
    /// y se responde con 409. Solo se elimina un hogar sin datos financieros (por
    /// ejemplo, recién creado con sus categorías del sistema). Los miembros se
    /// desvinculan del hogar; sus cuentas no se eliminan.
    /// </remarks>
    /// <param name="id">Identificador del hogar.</param>
    /// <response code="204">El hogar fue eliminado.</response>
    /// <response code="404">El hogar no existe o no pertenece al usuario actual.</response>
    /// <response code="409">La familia tiene datos financieros que se conservan.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var hogar = await _db.Hogares.FindAsync(id);
        if (hogar is null || hogar.IdHogar != User.GetIdHogar())
            return NotFound();

        // Requisito §6.3 Caso A: los datos financieros se conservan. Si la familia
        // tiene registros, no se borra el hogar (y la FK con RESTRICT impediría
        // cualquier cascada destructiva a nivel de base de datos).
        var tieneDatosFinancieros =
            await _db.Movimientos.AnyAsync(m => m.IdHogar == id) ||
            await _db.MetasAhorro.AnyAsync(m => m.IdHogar == id) ||
            await _db.Aportes.AnyAsync(a => a.IdHogar == id) ||
            await _db.Presupuestos.AnyAsync(p => p.IdHogar == id);

        if (tieneDatosFinancieros)
            return Conflict(new
            {
                message = "No se puede eliminar la familia: tiene datos financieros (movimientos, metas de ahorro, aportes o presupuestos) que se conservan. El hogar solo puede eliminarse si no registra datos financieros."
            });

        // La FK Usuario.IdHogar es RESTRICT: los miembros se desvinculan del hogar
        // en lugar de eliminarse junto con él.
        var miembros = await _db.Usuarios.Where(u => u.IdHogar == id).ToListAsync();
        foreach (var miembro in miembros)
        {
            miembro.IdHogar = null;
            miembro.Rol = null;
        }

        _db.Hogares.Remove(hogar);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static bool EsColisionCodigo(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.UniqueViolation
           && postgres.ConstraintName == "IX_Hogares_CodigoPublico";
}

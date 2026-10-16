using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

using RemesaSmartSV.DTOs;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoriasController(ApplicationDbContext db) => _db = db;

    /// <summary>Obtiene las categorías del hogar en páginas.</summary>
    /// <param name="page">Número de página que se consultará.</param>
    /// <param name="pageSize">Cantidad de categorías por página, entre 1 y 100.</param>
    /// <response code="200">Devuelve la página solicitada de categorías.</response>
    /// <response code="400">Los valores de paginación están fuera de los límites permitidos.</response>
    [ProducesResponseType(typeof(PaginatedResponse<Categoria>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Categoria>>> GetCategorias(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page debe ser mayor o igual a 1 y pageSize debe estar entre 1 y 100." });

        var idHogar = User.GetIdHogar();
        var query = _db.Categorias.Where(c => c.IdHogar == idHogar);

        var total = await query.AsNoTracking().CountAsync();
        var items = await query
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<Categoria>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>Obtiene una categoría del hogar por su identificador.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <response code="200">Devuelve la categoría encontrada.</response>
    /// <response code="404">La categoría no existe o no pertenece al hogar.</response>
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{id}")]
    public async Task<ActionResult<Categoria>> GetCategoria(int id)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        return categoria is null ? NotFound() : Ok(categoria);
    }

    /// <summary>Crea una categoría para el hogar actual.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Alimentación", "tipo": "Gasto", "icono": "shopping-cart"}</c></remarks>
    /// <param name="categoria">Datos de la categoría que se creará.</param>
    /// <response code="201">La categoría fue creada.</response>
    /// <response code="400">El usuario no tiene una familia vigente.</response>
    [ProducesResponseType(typeof(Categoria), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<Categoria>> Create([FromBody] Categoria categoria)
    {
        // La FK hacia Hogares es NOT NULL: sin hogar vigente el SaveChanges revienta en 500.
        var idHogar = await HogarVigenteAsync();
        if (idHogar is not > 0)
            return BadRequest(new { message = "Necesitás crear o unirte a una familia antes de registrar una categoría." });

        categoria.IdCategoria = 0;
        categoria.IdHogar = idHogar.Value;
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategoria), new { id = categoria.IdCategoria }, categoria);
    }

    /// <summary>Actualiza los datos de una categoría del hogar.</summary>
    /// <remarks>Ejemplo de cuerpo: <c>{"nombre": "Alimentación", "tipo": "Gasto", "icono": "shopping-cart"}</c></remarks>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="input">Nuevos datos de la categoría.</param>
    /// <response code="204">La categoría fue actualizada.</response>
    /// <response code="404">La categoría no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Categoria input)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        if (categoria is null)
            return NotFound();
        categoria.Nombre = input.Nombre;
        categoria.Tipo = input.Tipo;
        categoria.Icono = input.Icono;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Elimina una categoría del hogar.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <response code="204">La categoría fue eliminada.</response>
    /// <response code="404">La categoría no existe o no pertenece al hogar.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id && c.IdHogar == User.GetIdHogar());
        if (categoria is null)
            return NotFound();
        _db.Categorias.Remove(categoria);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Hogar vigente del usuario en la base; null si no tiene familia.</summary>
    private async Task<int?> HogarVigenteAsync()
        // Usa la membresía vigente en la base, no un claim IdHogar que podría estar obsoleto
        // (p. ej. tras abandonar la familia, el token viejo sigue trayendo el id viejo).
        => await _db.Usuarios.AsNoTracking()
            .Where(u => u.IdUsuario == User.GetIdUsuario())
            .Select(u => u.IdHogar)
            .FirstOrDefaultAsync();
}
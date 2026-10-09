using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

// Notificaciones in-app del usuario autenticado: listar (paginado), contar las no
// leídas para el badge, y marcar lectura. Solo el dueño ve o marca sus notificaciones.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotificacionesController(ApplicationDbContext db) => _db = db;

    /// <summary>Lista las notificaciones del usuario autenticado, de la más reciente hacia atrás.</summary>
    [ProducesResponseType(typeof(NotificacionesResponseDto), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<NotificacionesResponseDto>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page debe ser mayor o igual a 1 y pageSize debe estar entre 1 y 100." });

        var idUsuario = User.GetIdUsuario();
        var query = _db.Notificaciones
            .AsNoTracking()
            .Where(n => n.IdUsuario == idUsuario);

        var total = await query.CountAsync();
        var sinLeer = await query.CountAsync(n => !n.Leida);
        var items = await query
            .OrderByDescending(n => n.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new NotificacionesResponseDto(items, total, sinLeer, page, pageSize));
    }

    /// <summary>Marca una notificación del usuario como leída.</summary>
    /// <response code="204">Marcada.</response>
    /// <response code="404">No existe o no pertenece al usuario autenticado.</response>
    [HttpPut("{id}/marcar-leida")]
    public async Task<IActionResult> MarcarLeida(int id)
    {
        var notificacion = await _db.Notificaciones
            .FirstOrDefaultAsync(n => n.IdNotificacion == id && n.IdUsuario == User.GetIdUsuario());

        if (notificacion is null)
            return NotFound();

        notificacion.Leida = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Marca todas las notificaciones del usuario como leídas.</summary>
    [HttpPut("marcar-todas-leidas")]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var idUsuario = User.GetIdUsuario();
        var pendientes = await _db.Notificaciones
            .Where(n => n.IdUsuario == idUsuario && !n.Leida)
            .ToListAsync();

        foreach (var noti in pendientes)
            noti.Leida = true;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

// Invitaciones de familia: el Admin invita por correo; el invitado ve la invitacion
// en su campana de notificaciones y puede aceptarla o rechazarla. Solo el invitado
// puede responder; todo se valida en backend.
// El "aceptar" cambia IdHogar, asi que devuelve el LoginResponse refrescado con
// token nuevo (mismo contrato que POST api/Hogares).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvitacionesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuthService _authService;
    private readonly IEmailService _email;

    public InvitacionesController(ApplicationDbContext db, AuthService authService, IEmailService email)
    {
        _db = db;
        _authService = authService;
        _email = email;
    }

    /// <summary>Lista las invitaciones pendientes del usuario autenticado (por id asignado o por correo).</summary>
    [ProducesResponseType(typeof(IEnumerable<InvitacionFamilia>), StatusCodes.Status200OK)]
    [HttpGet("mias")]
    public async Task<ActionResult<IEnumerable<InvitacionFamilia>>> MisInvitaciones()
    {
        var idUsuario = User.GetIdUsuario();
        var correo = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        var invitaciones = await _db.InvitacionesFamilia
            .AsNoTracking()
            .Where(i => i.Estado == InvitacionFamilia.EstadoPendiente
                && (i.IdInvitado == idUsuario
                    || (i.IdInvitado == null && i.Correo.ToLower() == correo.ToLower())))
            .OrderBy(i => i.FechaEnvio)
            .ToListAsync();

        return Ok(invitaciones);
    }

    /// <summary>Lista las invitaciones enviadas al hogar actual con su estado.</summary>
    [ProducesResponseType(typeof(IEnumerable<InvitacionFamilia>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvitacionFamilia>>> Enviadas()
    {
        var invitaciones = await _db.InvitacionesFamilia
            .AsNoTracking()
            .Where(i => i.IdHogar == User.GetIdHogar())
            .OrderByDescending(i => i.FechaEnvio)
            .ToListAsync();
        return Ok(invitaciones);
    }

    /// <summary>Invita a un correo a pertenecer al hogar actual (solo Admin).</summary>
    /// <response code="201">La invitacion quedo pendiente y se notifico si existe la cuenta.</response>
    /// <response code="400">El correo ya pertenece al hogar.</response>
    /// <response code="409">Ya existe una invitacion pendiente para ese correo.</response>
    /// <response code="403">El usuario autenticado no es Admin del hogar.</response>
    [ProducesResponseType(typeof(InvitacionFamilia), StatusCodes.Status201Created)]
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InvitacionFamilia>> Invitar([FromBody] InvitarRequest request)
    {
        var idHogar = User.GetIdHogar();
        if (idHogar == 0)
            return Forbid();

        var correo = (request.Correo ?? string.Empty).Trim().ToLowerInvariant();

        if (await _db.Usuarios.AnyAsync(u => u.Correo.ToLower() == correo && u.IdHogar == idHogar))
            return BadRequest(new { message = "Este usuario ya es miembro del hogar." });

        var existente = await _db.InvitacionesFamilia.AnyAsync(i =>
            i.Estado == InvitacionFamilia.EstadoPendiente && i.Correo.ToLower() == correo);
        if (existente)
            return Conflict(new { message = "Ya existe una invitacion pendiente para este correo." });

        var invitado = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);

        var invitacion = new InvitacionFamilia
        {
            IdHogar = idHogar,
            IdInvitador = User.GetIdUsuario(),
            IdInvitado = invitado?.IdUsuario,
            Correo = correo,
            Estado = InvitacionFamilia.EstadoPendiente,
            FechaEnvio = DateTime.UtcNow,
        };

        _db.InvitacionesFamilia.Add(invitacion);

        // Si la cuenta existe, la notificacion llega de una; si no, la campana la
        // resuelve igual porque "mias" casa tambien por correo.
        if (invitado != null)
        {
            _db.Notificaciones.Add(new Notificacion
            {
                IdUsuario = invitado.IdUsuario,
                Tipo = Notificacion.TipoInvitacion,
                Mensaje = "Te invitaron a unirte a un hogar familiar en RemesaSmart SV.",
                Leida = false,
            });
        }

        await _db.SaveChangesAsync();

        // Envía correo de invitación (no bloquea si falla — la invitación queda en BD).
        var nombreHogar = await _db.Hogares
            .Where(h => h.IdHogar == idHogar)
            .Select(h => h.NombreFamiliar)
            .FirstOrDefaultAsync() ?? "una familia";
        var nombreInvitador = await _db.Usuarios
            .Where(u => u.IdUsuario == User.GetIdUsuario())
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync() ?? "Alguien";

        _ = _email.EnviarInvitacionFamiliarAsync(correo, nombreHogar, nombreInvitador, invitacion.IdInvitacion.ToString());

        return CreatedAtAction(nameof(MisInvitaciones), new { }, invitacion);
    }

    /// <summary>Acepta una invitacion: el usuario pasa a ser Miembro del hogar invitador.</summary>
    /// <response code="200">LoginResponse refrescado con el nuevo hogar y rol Miembro.</response>
    /// <response code="403">La invitacion no es del usuario autenticado.</response>
    /// <response code="409">El usuario ya pertenece a un hogar.</response>
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [HttpPost("{id}/aceptar")]
    public async Task<ActionResult<LoginResponse>> Aceptar(int id)
    {
        var idUsuario = User.GetIdUsuario();

        var invitacion = await _db.InvitacionesFamilia
            .FirstOrDefaultAsync(i => i.IdInvitacion == id);
        if (invitacion is null || invitacion.Estado != InvitacionFamilia.EstadoPendiente)
            return NotFound();

        // Solo el destinatario puede responder: por id asignado o, si quedo libre,
        // por correo registrado (cuenta creada despues de la invitacion).
        var dirigida = invitacion.IdInvitado == idUsuario;
        if (!dirigida && invitacion.IdInvitado == null)
        {
            var correo = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            dirigida = invitacion.Correo.ToLower() == correo.ToLower();
        }
        if (!dirigida)
            return Forbid();

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario is null)
            return NotFound();

        if (usuario.IdHogar != null && usuario.IdHogar != 0)
            return Conflict(new { message = "Ya perteneces a un hogar. Solo puedes pertenecer a uno." });

        invitacion.Estado = InvitacionFamilia.EstadoAceptada;
        invitacion.FechaRespuesta = DateTime.UtcNow;
        invitacion.IdInvitado = idUsuario;

        usuario.IdHogar = invitacion.IdHogar;
        usuario.Rol = "Miembro";

        _db.Notificaciones.Add(new Notificacion
        {
            IdUsuario = invitacion.IdInvitador,
            Tipo = Notificacion.TipoSistema,
            Mensaje = $"'{usuario.Nombre}' acepto la invitacion y se unio a tu hogar.",
            Leida = false,
        });

        await _db.SaveChangesAsync();

        return Ok(_authService.BuildLoginResponse(usuario));
    }

    /// <summary>Rechaza una invitacion pendiente.</summary>
    /// <response code="204">La invitacion quedo rechazada.</response>
    /// <response code="403">La invitacion no es del usuario autenticado.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("{id}/rechazar")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var idUsuario = User.GetIdUsuario();

        var invitacion = await _db.InvitacionesFamilia
            .FirstOrDefaultAsync(i => i.IdInvitacion == id);
        if (invitacion is null || invitacion.Estado != InvitacionFamilia.EstadoPendiente)
            return NotFound();

        var dirigida = invitacion.IdInvitado == idUsuario;
        if (!dirigida && invitacion.IdInvitado == null)
        {
            var correo = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
            dirigida = invitacion.Correo.ToLower() == correo.ToLower();
        }
        if (!dirigida)
            return Forbid();

        invitacion.Estado = InvitacionFamilia.EstadoRechazada;
        invitacion.FechaRespuesta = DateTime.UtcNow;
        if (invitacion.IdInvitado == null)
            invitacion.IdInvitado = idUsuario;

        _db.Notificaciones.Add(new Notificacion
        {
            IdUsuario = invitacion.IdInvitador,
            Tipo = Notificacion.TipoSistema,
            Mensaje = "Una invitacion al hogar fue rechazada.",
            Leida = false,
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Cancela una invitación pendiente (solo Admin del hogar).</summary>
    /// <response code="204">La invitación fue cancelada.</response>
    /// <response code="403">El usuario no es Admin del hogar.</response>
    /// <response code="404">La invitación no existe o no está pendiente.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("{id}/cancelar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var invitacion = await _db.InvitacionesFamilia
            .FirstOrDefaultAsync(i => i.IdInvitacion == id && i.IdHogar == User.GetIdHogar());
        if (invitacion is null || invitacion.Estado != InvitacionFamilia.EstadoPendiente)
            return NotFound();

        invitacion.Estado = InvitacionFamilia.EstadoCancelada;
        invitacion.FechaRespuesta = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Reenvía una invitación pendiente (actualiza la fecha de envío).</summary>
    /// <response code="204">La invitación fue reenviada.</response>
    /// <response code="403">El usuario no es Admin del hogar.</response>
    /// <response code="404">La invitación no existe o no está pendiente.</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("{id}/reenviar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reenviar(int id)
    {
        var invitacion = await _db.InvitacionesFamilia
            .FirstOrDefaultAsync(i => i.IdInvitacion == id && i.IdHogar == User.GetIdHogar());
        if (invitacion is null || invitacion.Estado != InvitacionFamilia.EstadoPendiente)
            return NotFound();

        invitacion.FechaEnvio = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

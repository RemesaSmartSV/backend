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
[EnableRateLimiting("auth")]
public class SolicitudesUnionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;

    public SolicitudesUnionController(ApplicationDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    /// <summary>Envía una solicitud para unirse a una familia, identificada por su código público o por su id.</summary>
    /// <remarks>El cuerpo debe traer exactamente uno de los dos campos: <c>codigoPublico</c> (flujo de código)
    /// o <c>idHogar</c> (flujo de búsqueda por nombre, que no expone el código público).</remarks>
    /// <param name="request">Código público o identificador de la familia; exactamente uno de los dos.</param>
    /// <response code="201">La solicitud fue creada y queda pendiente de aprobación del Admin.</response>
    /// <response code="400">Vinieron ambos campos, ninguno, o el código no tiene formato válido.</response>
    /// <response code="404">No existe una familia con ese código o con ese identificador.</response>
    /// <response code="409">Ya pertenece a una familia, ya hay solicitud pendiente o la familia no tiene Admin activo.</response>
    [ProducesResponseType(typeof(SolicitudUnionPropiaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<ActionResult<SolicitudUnionPropiaDto>> Crear([FromBody] CrearSolicitudUnionRequest request)
    {
        var solicitante = await UsuarioActualAsync();
        if (solicitante is null)
            return Unauthorized();
        if (solicitante.IdHogar is > 0)
            return Conflict(new { message = "Ya perteneces a una familia. Abandónala antes de solicitar otra." });

        var tieneCodigo = !string.IsNullOrWhiteSpace(request.CodigoPublico);
        var tieneId = request.IdHogar is > 0;
        if (tieneCodigo && tieneId)
            return BadRequest(new { message = "Indica solo un campo: codigoPublico o idHogar, no ambos." });
        if (!tieneCodigo && !tieneId)
            return BadRequest(new { message = "Debes indicar codigoPublico o idHogar para elegir la familia." });

        Hogar? hogar;
        if (tieneId)
        {
            // Caso búsqueda por nombre: el resultado no trae el código, solo el id.
            hogar = await _db.Hogares.AsNoTracking()
                .FirstOrDefaultAsync(h => h.IdHogar == request.IdHogar);
            if (hogar is null)
                return NotFound(new { message = "No se encontró una familia con ese identificador." });
        }
        else
        {
            if (!CodigoHogar.EsValido(request.CodigoPublico!))
                return BadRequest(new { message = "El código de familia no tiene un formato válido." });

            var codigo = CodigoHogar.Normalizar(request.CodigoPublico!);
            hogar = await _db.Hogares.AsNoTracking().FirstOrDefaultAsync(h => h.CodigoPublico == codigo);
            if (hogar is null)
                return NotFound(new { message = "No se encontró una familia con ese código." });
        }

        var pendiente = await _db.SolicitudesUnion.AnyAsync(s =>
            s.IdHogar == hogar.IdHogar && s.IdUsuario == solicitante.IdUsuario && s.Estado == SolicitudUnion.Pendiente);
        if (pendiente)
            return Conflict(new { message = "Ya tienes una solicitud pendiente para esta familia." });

        var admins = await _db.Usuarios.AsNoTracking()
            .Where(u => u.IdHogar == hogar.IdHogar && u.Rol == "Admin")
            .Select(u => u.IdUsuario)
            .ToListAsync();
        if (admins.Count == 0)
            return Conflict(new { message = "Esta familia no tiene un administrador activo que pueda revisar la solicitud." });

        var solicitud = new SolicitudUnion
        {
            IdHogar = hogar.IdHogar,
            IdUsuario = solicitante.IdUsuario,
            Estado = SolicitudUnion.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
        };

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

        try
        {
            _db.SolicitudesUnion.Add(solicitud);
            await _db.SaveChangesAsync();

            foreach (var idAdmin in admins)
            {
                _db.Notificaciones.Add(new Notificacion
                {
                    IdUsuario = idAdmin,
                    Tipo = Notificacion.TipoSolicitudUnion,
                    Mensaje = $"{solicitante.Nombre} solicitó unirse a la familia {hogar.NombreFamiliar}.",
                    Leida = false,
                    IdReferencia = solicitud.IdSolicitud,
                });
            }

            await _db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();

            return CreatedAtAction(nameof(Mias), new { }, new SolicitudUnionPropiaDto(
                solicitud.IdSolicitud, hogar.CodigoPublico, hogar.NombreFamiliar,
                solicitud.Estado, solicitud.FechaSolicitud, solicitud.FechaRespuesta));
        }
        catch (DbUpdateException ex) when (EsSolicitudPendienteDuplicada(ex))
        {
            if (transaction is not null) await transaction.RollbackAsync();
            return Conflict(new { message = "Ya tienes una solicitud pendiente para esta familia." });
        }
    }

    /// <summary>Lista las solicitudes enviadas por el usuario actual.</summary>
    [HttpGet("mias")]
    public async Task<ActionResult<IEnumerable<SolicitudUnionPropiaDto>>> Mias()
    {
        var idUsuario = User.GetIdUsuario();
        var solicitudes = await _db.SolicitudesUnion.AsNoTracking()
            .Where(s => s.IdUsuario == idUsuario)
            .Include(s => s.Hogar)
            .OrderByDescending(s => s.FechaSolicitud)
            .Select(s => new SolicitudUnionPropiaDto(
                s.IdSolicitud, s.Hogar.CodigoPublico, s.Hogar.NombreFamiliar,
                s.Estado, s.FechaSolicitud, s.FechaRespuesta))
            .ToListAsync();
        return Ok(solicitudes);
    }

    /// <summary>Lista solicitudes pendientes del hogar del Admin autenticado.</summary>
    [HttpGet("recibidas")]
    public async Task<ActionResult<IEnumerable<SolicitudUnionAdminDto>>> Recibidas()
    {
        var admin = await AdminActualAsync();
        if (admin is null)
            return Forbid();

        var solicitudes = await _db.SolicitudesUnion.AsNoTracking()
            .Where(s => s.IdHogar == admin.IdHogar && s.Estado == SolicitudUnion.Pendiente)
            .Include(s => s.Usuario)
            .OrderBy(s => s.FechaSolicitud)
            .Select(s => new SolicitudUnionAdminDto(
                s.IdSolicitud, s.Usuario.NombreVisible ?? s.Usuario.Nombre,
                s.Usuario.Correo, s.Estado, s.FechaSolicitud))
            .ToListAsync();
        return Ok(solicitudes);
    }

    /// <summary>Acepta una solicitud pendiente y agrega al solicitante como Miembro.</summary>
    [HttpPost("{id:int}/aceptar")]
    public async Task<IActionResult> Aceptar(int id)
    {
        var admin = await AdminActualAsync();
        if (admin is null)
            return Forbid();

        var solicitud = await _db.SolicitudesUnion.AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdSolicitud == id && s.IdHogar == admin.IdHogar && s.Estado == SolicitudUnion.Pendiente);
        if (solicitud is null)
            return NotFound();

        var solicitante = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == solicitud.IdUsuario);
        if (solicitante is null)
            return NotFound();
        if (solicitante.IdHogar is > 0)
            return Conflict(new { message = "El usuario ya pertenece a una familia." });

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

        if (_db.Database.IsRelational())
        {
            var unido = await _db.Usuarios
                .Where(u => u.IdUsuario == solicitante.IdUsuario && (u.IdHogar == null || u.IdHogar == 0))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(u => u.IdHogar, admin.IdHogar)
                    .SetProperty(u => u.Rol, "Miembro"));
            var actualizada = await _db.SolicitudesUnion
                .Where(s => s.IdSolicitud == id && s.IdHogar == admin.IdHogar && s.Estado == SolicitudUnion.Pendiente)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(s => s.Estado, SolicitudUnion.Aceptada)
                    .SetProperty(s => s.FechaRespuesta, DateTime.UtcNow)
                    .SetProperty(s => s.IdAdminRespuesta, admin.IdUsuario));

            if (unido != 1 || actualizada != 1)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                return Conflict(new { message = "La solicitud ya fue atendida o el usuario ya pertenece a una familia." });
            }
        }
        else
        {
            var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == solicitante.IdUsuario && u.IdHogar == null);
            var trackedRequest = await _db.SolicitudesUnion.FirstOrDefaultAsync(s => s.IdSolicitud == id && s.Estado == SolicitudUnion.Pendiente);
            if (user is null || trackedRequest is null)
                return Conflict(new { message = "La solicitud ya fue atendida o el usuario ya pertenece a una familia." });
            user.IdHogar = admin.IdHogar;
            user.Rol = "Miembro";
            trackedRequest.Estado = SolicitudUnion.Aceptada;
            trackedRequest.FechaRespuesta = DateTime.UtcNow;
            trackedRequest.IdAdminRespuesta = admin.IdUsuario;
        }

        _db.Notificaciones.Add(new Notificacion
        {
            IdUsuario = solicitante.IdUsuario,
            Tipo = Notificacion.TipoSolicitudUnion,
            Mensaje = "Tu solicitud para unirte a una familia fue aceptada. Actualiza tu sesión para acceder a ella.",
            Leida = false,
            IdReferencia = id,
        });
        await _db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        // Aviso al solicitante (fire-and-forget): el resultado ya quedó persistido.
        var nombreFamilia = await _db.Hogares.AsNoTracking()
            .Where(h => h.IdHogar == admin.IdHogar)
            .Select(h => h.NombreFamiliar)
            .FirstOrDefaultAsync() ?? "una familia";
        _ = _email.EnviarResultadoSolicitudUnionAsync(solicitante.Correo, solicitante.Nombre, nombreFamilia, aceptada: true);

        // Aviso de la nueva incorporación a todos los miembros del hogar,
        // incluida la persona que se acaba de unir. Los destinatarios se
        // materializan dentro del scope de la request; solo el envío corre
        // fuera de ella (fire-and-forget).
        var miembrosHogar = await PrepararAvisoUnionFamiliaAsync(admin.IdHogar ?? 0);
        _ = NotificarUnionFamiliaAsync(miembrosHogar, nombreFamilia, solicitante.Nombre);

        return NoContent();
    }

    /// <summary>Rechaza una solicitud pendiente del hogar del Admin autenticado.</summary>
    [HttpPost("{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var admin = await AdminActualAsync();
        if (admin is null)
            return Forbid();

        var solicitud = await _db.SolicitudesUnion
            .FirstOrDefaultAsync(s => s.IdSolicitud == id && s.IdHogar == admin.IdHogar && s.Estado == SolicitudUnion.Pendiente);
        if (solicitud is null)
            return NotFound();

        solicitud.Estado = SolicitudUnion.Rechazada;
        solicitud.FechaRespuesta = DateTime.UtcNow;
        solicitud.IdAdminRespuesta = admin.IdUsuario;
        _db.Notificaciones.Add(new Notificacion
        {
            IdUsuario = solicitud.IdUsuario,
            Tipo = Notificacion.TipoSolicitudUnion,
            Mensaje = "Tu solicitud para unirte a una familia fue rechazada.",
            Leida = false,
            IdReferencia = id,
        });
        await _db.SaveChangesAsync();

        // Aviso al solicitante (fire-and-forget): el resultado ya quedó persistido.
        var solicitante = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == solicitud.IdUsuario);
        if (solicitante is not null)
        {
            var nombreFamilia = await _db.Hogares.AsNoTracking()
                .Where(h => h.IdHogar == admin.IdHogar)
                .Select(h => h.NombreFamiliar)
                .FirstOrDefaultAsync() ?? "una familia";
            _ = _email.EnviarResultadoSolicitudUnionAsync(solicitante.Correo, solicitante.Nombre, nombreFamilia, aceptada: false);
        }
        return NoContent();
    }

    /// <summary>Cancela una solicitud propia que aún está pendiente.</summary>
    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var solicitud = await _db.SolicitudesUnion
            .FirstOrDefaultAsync(s => s.IdSolicitud == id && s.IdUsuario == User.GetIdUsuario() && s.Estado == SolicitudUnion.Pendiente);
        if (solicitud is null)
            return NotFound();

        solicitud.Estado = SolicitudUnion.Cancelada;
        solicitud.FechaRespuesta = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Usuario?> UsuarioActualAsync()
        => await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == User.GetIdUsuario());

    private async Task<Usuario?> AdminActualAsync()
    {
        var user = await UsuarioActualAsync();
        return user?.Rol == "Admin" && user.IdHogar is > 0 ? user : null;
    }

    private static bool EsSolicitudPendienteDuplicada(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
           && postgres.SqlState == PostgresErrorCodes.UniqueViolation
           && postgres.ConstraintName == "IX_SolicitudesUnion_IdHogar_IdUsuario";

    /// <summary>
    /// Materializa, dentro del scope de la request, los correos de los miembros
    /// del hogar. Devuelve una lista vacía si el hogar no existe o la consulta
    /// falla.
    /// </summary>
    private async Task<List<string>> PrepararAvisoUnionFamiliaAsync(int idHogar)
    {
        try
        {
            if (idHogar <= 0)
                return new List<string>();

            return await _db.Usuarios
                .AsNoTracking()
                .Where(u => u.IdHogar == idHogar)
                .Select(u => u.Correo)
                .ToListAsync();
        }
        catch
        {
            // La consulta del aviso no debe bloquear el alta del miembro.
            return new List<string>();
        }
    }

    /// <summary>
    /// Avisa por correo a los miembros del hogar que una persona se unió a la
    /// familia. No toca el DbContext: corre fuera del scope de la request.
    /// </summary>
    private async Task NotificarUnionFamiliaAsync(List<string> correos, string nombreFamilia, string nombrePersona)
    {
        try
        {
            foreach (var correo in correos)
            {
                await _email.EnviarUnionFamiliaAsync(correo, nombreFamilia, nombrePersona);
            }
        }
        catch
        {
            // El aviso de unión no debe bloquear el alta del miembro.
        }
    }
}

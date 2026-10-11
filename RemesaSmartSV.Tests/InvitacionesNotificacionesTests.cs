using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

public class InvitacionesNotificacionesTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public InvitacionesNotificacionesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static IConfiguration ConfigFalsa() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "clave-de-prueba-muy-larga-para-hmacsha256-0123456789",
            ["Jwt:Issuer"] = "RemesaSmartSV",
            ["Jwt:Audience"] = "RemesaSmartSVClient",
        }).Build();

    private static InvitacionesController CrearInvitaciones(
        ApplicationDbContext db,
        int idUsuario,
        int idHogar,
        string correo,
        string rol = "Admin")
    {
        var controller = new InvitacionesController(db, new AuthService(db, ConfigFalsa()));
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString()),
            new(ClaimTypes.Role, rol),
            new(ClaimTypes.Email, correo),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) },
        };
        return controller;
    }

    private static NotificacionesController CrearNotificaciones(
        ApplicationDbContext db,
        int idUsuario)
    {
        var controller = new NotificacionesController(db);
        var claims = new List<Claim> { new("idUsuario", idUsuario.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) },
        };
        return controller;
    }

    private async Task<Usuario> CrearUsuario(string correo, int? idHogar = null, string rol = "Miembro")
    {
        var usuario = new Usuario
        {
            Nombre = correo.Split('@')[0],
            Correo = correo,
            ContrasenaHash = "x",
            IdHogar = idHogar,
            Rol = rol,
            FechaRegistro = DateTime.UtcNow,
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    // ---------- Invitaciones ----------

    [Fact]
    public async Task Invitar_CorreoConCuenta_CreaInvitacionYNotificacion()
    {
        var invitado = await CrearUsuario("ana@demo.com");
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com");

        var resultado = await controller.Invitar(new InvitarRequest("ana@demo.com"));

        var created = Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var invitacion = Assert.IsType<InvitacionFamilia>(created.Value);
        Assert.Equal(InvitacionFamilia.EstadoPendiente, invitacion.Estado);
        Assert.Equal(invitado.IdUsuario, invitacion.IdInvitado);

        var notificaciones = await _db.Notificaciones.Where(n => n.IdUsuario == invitado.IdUsuario).ToListAsync();
        Assert.Single(notificaciones);
        Assert.Equal(Notificacion.TipoInvitacion, notificaciones[0].Tipo);
    }

    [Fact]
    public async Task Invitar_CorreoSinCuenta_QuedaPendienteSoloPorCorreo()
    {
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com");

        var resultado = await controller.Invitar(new InvitarRequest("nuevo@demo.com"));

        var created = Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var invitacion = Assert.IsType<InvitacionFamilia>(created.Value);
        Assert.Null(invitacion.IdInvitado);
        Assert.Equal(0, await _db.Notificaciones.CountAsync());
    }

    [Fact]
    public async Task Invitar_CorreoYaMiembro_RetornaBadRequest()
    {
        await CrearUsuario("miembro@demo.com", idHogar: 1);
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com");

        var resultado = await controller.Invitar(new InvitarRequest("miembro@demo.com"));

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Invitar_Duplicada_RetornaConflict()
    {
        await _db.InvitacionesFamilia.AddAsync(new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        });
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com");
        var resultado = await controller.Invitar(new InvitarRequest("ana@demo.com"));

        Assert.IsType<ConflictObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task MisInvitaciones_CasaPorIdOporCorreo()
    {
        var invitado = await CrearUsuario("ana@demo.com");
        await _db.InvitacionesFamilia.AddRangeAsync(
            new InvitacionFamilia { IdHogar = 1, IdInvitador = 1, IdInvitado = invitado.IdUsuario, Correo = "ana@demo.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 2, IdInvitador = 2, Correo = "otro@demo.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 3, IdInvitador = 3, Correo = "ana@demo.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 4, IdInvitador = 4, Correo = "ana@demo.com", Estado = InvitacionFamilia.EstadoRechazada }
        );
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@demo.com", rol: "Miembro");
        var resultado = await controller.MisInvitaciones();

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<InvitacionFamilia>>(ok.Value).ToList();

        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.IdHogar == 4);
        Assert.DoesNotContain(items, i => i.IdHogar == 2);
    }

    [Fact]
    public async Task Aceptar_SinHogarPrevio_SeUneComoMiembroYNotificaAlInvitador()
    {
        var invitado = await CrearUsuario("ana@demo.com", idHogar: null);
        await _db.Hogares.AddAsync(new Hogar { NombreFamiliar = "Familia Admin" });
        await _db.SaveChangesAsync();
        var hogar = _db.Hogares.Local.First();
        var invitacion = new InvitacionFamilia
        {
            IdHogar = hogar.IdHogar,
            IdInvitador = 900,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@demo.com", rol: "Miembro");
        var resultado = await controller.Aceptar(invitacion.IdInvitacion);

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var login = Assert.IsType<LoginResponse>(ok.Value);
        Assert.False(string.IsNullOrEmpty(login.Token));
        Assert.Equal(hogar.IdHogar, login.IdHogar);
        Assert.Equal("Miembro", login.Rol);

        var usuarioDb = await _db.Usuarios.FirstAsync(u => u.IdUsuario == invitado.IdUsuario);
        Assert.Equal(hogar.IdHogar, usuarioDb.IdHogar);
        Assert.Equal(InvitacionFamilia.EstadoAceptada, invitacion.Estado);
        Assert.NotNull(invitacion.FechaRespuesta);
        Assert.Equal(1, await _db.Notificaciones.CountAsync(n => n.IdUsuario == 900));
    }

    [Fact]
    public async Task Aceptar_YaTieneHogar_RetornaConflict()
    {
        await _db.Hogares.AddAsync(new Hogar { NombreFamiliar = "Otro hogar" });
        await _db.SaveChangesAsync();
        var invitado = await CrearUsuario("ana@demo.com", idHogar: _db.Hogares.Local.First().IdHogar);
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 55,
            IdInvitador = 900,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: invitado.IdHogar ?? 0, correo: "ana@demo.com");
        var resultado = await controller.Aceptar(invitacion.IdInvitacion);

        Assert.IsType<ConflictObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Aceptar_NoEsElInvitado_RetornaForbid()
    {
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 2,
            Correo = "otrapersona@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 77, idHogar: 0, correo: "espi@demo.com", rol: "Miembro");
        var resultado = await controller.Aceptar(invitacion.IdInvitacion);

        Assert.IsType<ForbidResult>(resultado.Result);
    }

    [Fact]
    public async Task Rechazar_MarcaRechazadaYNotificaAlInvitador()
    {
        var invitado = await CrearUsuario("ana@demo.com");
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 900,
            IdInvitado = invitado.IdUsuario,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@demo.com");
        var resultado = await controller.Rechazar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(InvitacionFamilia.EstadoRechazada, invitacion.Estado);
        Assert.NotNull(invitacion.FechaRespuesta);
        Assert.Equal(1, await _db.Notificaciones.CountAsync(n => n.IdUsuario == 900));
    }

    // ---------- Notificaciones ----------

    [Fact]
    public async Task Get_SoloDelUsuarioYContaNoLeidas()
    {
        _db.Notificaciones.AddRange(
            new Notificacion { IdUsuario = 1, Tipo = Notificacion.TipoSistema, Mensaje = "mia", Leida = false },
            new Notificacion { IdUsuario = 1, Tipo = Notificacion.TipoSistema, Mensaje = "mia leida", Leida = true },
            new Notificacion { IdUsuario = 2, Tipo = Notificacion.TipoSistema, Mensaje = "de otra" }
        );
        await _db.SaveChangesAsync();

        var controller = CrearNotificaciones(_db, idUsuario: 1);
        var resultado = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<NotificacionesResponseDto>(ok.Value);
        Assert.Equal(2, dto.Total);
        Assert.Equal(1, dto.SinLeer);
        Assert.Equal(2, dto.Items.Count);
    }

    [Fact]
    public async Task MarcarLeida_DeOtroUsuario_RetornaNotFound()
    {
        var ajena = new Notificacion { IdUsuario = 2, Tipo = Notificacion.TipoSistema, Mensaje = "de otra" };
        _db.Notificaciones.Add(ajena);
        await _db.SaveChangesAsync();

        var controller = CrearNotificaciones(_db, idUsuario: 1);
        var resultado = await controller.MarcarLeida(ajena.IdNotificacion);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task MarcarLeida_Propia_MarcaYDevuelveNoContent()
    {
        var propia = new Notificacion { IdUsuario = 1, Tipo = Notificacion.TipoSistema, Mensaje = "mia", Leida = false };
        _db.Notificaciones.Add(propia);
        await _db.SaveChangesAsync();

        var controller = CrearNotificaciones(_db, idUsuario: 1);
        var resultado = await controller.MarcarLeida(propia.IdNotificacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.True(propia.Leida);
    }

    [Fact]
    public async Task MarcarTodasLeidas_MarcaSoloLasDelUsuario()
    {
        _db.Notificaciones.AddRange(
            new Notificacion { IdUsuario = 1, Tipo = Notificacion.TipoSistema, Mensaje = "a", Leida = false },
            new Notificacion { IdUsuario = 1, Tipo = Notificacion.TipoSistema, Mensaje = "b", Leida = false },
            new Notificacion { IdUsuario = 2, Tipo = Notificacion.TipoSistema, Mensaje = "ajena", Leida = false }
        );
        await _db.SaveChangesAsync();

        var controller = CrearNotificaciones(_db, idUsuario: 1);
        var resultado = await controller.MarcarTodasLeidas();

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(2, await _db.Notificaciones.CountAsync(n => n.IdUsuario == 1 && n.Leida));
        Assert.Equal(0, await _db.Notificaciones.CountAsync(n => n.IdUsuario == 2 && n.Leida));
    }

    // ---------- Protecciones de familia ----------

    [Fact]
    public async Task Cancelar_InvitacionPendiente_LaMarcaCancelada()
    {
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com", rol: "Admin");
        var resultado = await controller.Cancelar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(InvitacionFamilia.EstadoCancelada, invitacion.Estado);
    }

    [Fact]
    public async Task Cancelar_InvitacionAceptada_RetornaNotFound()
    {
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoAceptada,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com", rol: "Admin");
        var resultado = await controller.Cancelar(invitacion.IdInvitacion);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task Reenviar_InvitacionPendiente_ActualizaFecha()
    {
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@demo.com",
            Estado = InvitacionFamilia.EstadoPendiente,
            FechaEnvio = DateTime.UtcNow.AddDays(-5),
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var fechaAnterior = invitacion.FechaEnvio;
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@demo.com", rol: "Admin");
        var resultado = await controller.Reenviar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.True(invitacion.FechaEnvio > fechaAnterior);
    }
}


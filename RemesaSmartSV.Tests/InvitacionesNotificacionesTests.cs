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

    /// <summary>
    /// Stub de IEmailService que registra cada envío como (destino, asunto) para
    /// assertar el cableado de notificaciones sin depender del SMTP real.
    /// </summary>
    private class EmailStub : Services.IEmailService
    {
        public List<(string Destino, string Asunto)> Enviados { get; } = new();

        public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
        { Enviados.Add((correoDestino, "invitacion-familiar")); return Task.FromResult(true); }
        public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
        { Enviados.Add((correoDestino, "reset-contrasena")); return Task.FromResult(true); }
        public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
        { Enviados.Add((correoDestino, "alerta-presupuesto")); return Task.FromResult(true); }
        public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
        { Enviados.Add((correoDestino, "presupuesto-excedido")); return Task.FromResult(true); }
        public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
        { Enviados.Add((correoDestino, "notificacion-cuenta")); return Task.FromResult(true); }
        public Task<bool> EnviarAvisoAccesoAsync(string correoDestino, DateTime fechaUtc, string? origen)
        { Enviados.Add((correoDestino, "aviso-acceso")); return Task.FromResult(true); }
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string correoDestino, DateTime fechaUtc)
        { Enviados.Add((correoDestino, "aviso-cambio-contrasena")); return Task.FromResult(true); }
        public Task<bool> EnviarResultadoSolicitudUnionAsync(string correoDestino, string nombreSolicitante, string nombreFamilia, bool aceptada)
        { Enviados.Add((correoDestino, "resultado-solicitud-union")); return Task.FromResult(true); }
        public Task<bool> EnviarResultadoInvitacionAsync(string correoDestino, string nombreFamilia, string nombreInvitado, bool aceptada)
        { Enviados.Add((correoDestino, "resultado-invitacion")); return Task.FromResult(true); }
        public Task<bool> EnviarMovimientoFamiliaAsync(string correoDestino, string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
        { Enviados.Add((correoDestino, "movimiento-familia")); return Task.FromResult(true); }
        public Task<bool> EnviarUnionFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
        { Enviados.Add((correoDestino, "union-familia")); return Task.FromResult(true); }
        public Task<bool> EnviarSalidaFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
        { Enviados.Add((correoDestino, "salida-familia")); return Task.FromResult(true); }
    }

    private static InvitacionesController CrearInvitaciones(
        ApplicationDbContext db,
        int idUsuario,
        int idHogar,
        string correo,
        string rol = "Admin",
        EmailStub? email = null)
    {
        var controller = new InvitacionesController(db, new AuthService(db, ConfigFalsa()), email ?? new EmailStub());
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
        var invitado = await CrearUsuario("ana@gmail.com");
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");

        var resultado = await controller.Invitar(new InvitarRequest("ana@gmail.com"));

        var created = Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var invitacion = Assert.IsType<InvitacionFamilia>(created.Value);
        Assert.Equal(InvitacionFamilia.EstadoPendiente, invitacion.Estado);
        Assert.Equal(invitado.IdUsuario, invitacion.IdInvitado);

        var notificaciones = await _db.Notificaciones.Where(n => n.IdUsuario == invitado.IdUsuario).ToListAsync();
        Assert.Single(notificaciones);
        Assert.Equal(Notificacion.TipoInvitacion, notificaciones[0].Tipo);
    }

    [Fact]
    public async Task Invitar_DominioNoGmail_RetornaBadRequest()
    {
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");

        var resultado = await controller.Invitar(new InvitarRequest("persona@example.org"));

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Empty(await _db.InvitacionesFamilia.ToListAsync());
    }

    [Fact]
    public async Task Invitar_CorreoSinCuenta_QuedaPendienteSoloPorCorreo()
    {
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");

        var resultado = await controller.Invitar(new InvitarRequest("nuevo@gmail.com"));

        var created = Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var invitacion = Assert.IsType<InvitacionFamilia>(created.Value);
        Assert.Null(invitacion.IdInvitado);
        Assert.Equal(0, await _db.Notificaciones.CountAsync());
    }

    [Fact]
    public async Task Invitar_CorreoYaMiembro_RetornaBadRequest()
    {
        await CrearUsuario("miembro@gmail.com", idHogar: 1);
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");

        var resultado = await controller.Invitar(new InvitarRequest("miembro@gmail.com"));

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Invitar_Duplicada_RetornaConflict()
    {
        await _db.InvitacionesFamilia.AddAsync(new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        });
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");
        var resultado = await controller.Invitar(new InvitarRequest("ana@gmail.com"));

        Assert.IsType<ConflictObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Invitar_TrasInvitacionRechazada_PermiteReinvitar()
    {
        // Invitación previa RECHAZADA al mismo correo en el mismo hogar: ya no
        // compite por la unicidad, así que el chequeo no debe bloquearla.
        await _db.InvitacionesFamilia.AddAsync(new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoRechazada,
        });
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com");
        var resultado = await controller.Invitar(new InvitarRequest("ana@gmail.com"));

        Assert.IsType<CreatedAtActionResult>(resultado.Result);
        Assert.Equal(2, await _db.InvitacionesFamilia.CountAsync());
        Assert.Single(await _db.InvitacionesFamilia
            .Where(i => i.Estado == InvitacionFamilia.EstadoPendiente).ToListAsync());
    }

    /// <summary>
    /// Verifica la configuración del índice unique de invitaciones: solo las
    /// filas con Estado = 'Pendiente' compiten por (IdHogar, Correo). Es una
    /// aserción de modelo, no de BD: EF InMemory construye el modelo pero no
    /// ejecuta los índices; el comportamiento real se valida en PostgreSQL.
    /// </summary>
    [Fact]
    public void Modelo_IndiceDeInvitacionesEsUnicoSoloParaPendientes()
    {
        var entidad = _db.Model.FindEntityType(typeof(InvitacionFamilia));
        Assert.NotNull(entidad);

        var indice = entidad!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "IdHogar", "Correo" }));

        Assert.NotNull(indice);
        Assert.True(indice!.IsUnique);
        Assert.Equal("\"Estado\" = 'Pendiente'", indice.GetFilter());
    }

    [Fact]
    public async Task MisInvitaciones_CasaPorIdOporCorreo()
    {
        var invitado = await CrearUsuario("ana@gmail.com");
        await _db.InvitacionesFamilia.AddRangeAsync(
            new InvitacionFamilia { IdHogar = 1, IdInvitador = 1, IdInvitado = invitado.IdUsuario, Correo = "ana@gmail.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 2, IdInvitador = 2, Correo = "otro@gmail.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 3, IdInvitador = 3, Correo = "ana@gmail.com", Estado = InvitacionFamilia.EstadoPendiente },
            new InvitacionFamilia { IdHogar = 4, IdInvitador = 4, Correo = "ana@gmail.com", Estado = InvitacionFamilia.EstadoRechazada }
        );
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@gmail.com", rol: "Miembro");
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
        var invitado = await CrearUsuario("ana@gmail.com", idHogar: null);
        await _db.Hogares.AddAsync(new Hogar { NombreFamiliar = "Familia Admin" });
        await _db.SaveChangesAsync();
        var hogar = _db.Hogares.Local.First();
        var invitacion = new InvitacionFamilia
        {
            IdHogar = hogar.IdHogar,
            IdInvitador = 900,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@gmail.com", rol: "Miembro");
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
        var invitado = await CrearUsuario("ana@gmail.com", idHogar: _db.Hogares.Local.First().IdHogar);
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 55,
            IdInvitador = 900,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: invitado.IdHogar ?? 0, correo: "ana@gmail.com");
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
            Correo = "otrapersona@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 77, idHogar: 0, correo: "espi@gmail.com", rol: "Miembro");
        var resultado = await controller.Aceptar(invitacion.IdInvitacion);

        Assert.IsType<ForbidResult>(resultado.Result);
    }

    [Fact]
    public async Task Rechazar_MarcaRechazadaYNotificaAlInvitador()
    {
        var invitado = await CrearUsuario("ana@gmail.com");
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 900,
            IdInvitado = invitado.IdUsuario,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@gmail.com");
        var resultado = await controller.Rechazar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(InvitacionFamilia.EstadoRechazada, invitacion.Estado);
        Assert.NotNull(invitacion.FechaRespuesta);
        Assert.Equal(1, await _db.Notificaciones.CountAsync(n => n.IdUsuario == 900));
    }

    [Fact]
    public async Task Aceptar_NotificaPorCorreoAlInvitador()
    {
        var invitado = await CrearUsuario("ana@gmail.com");
        var invitador = await CrearUsuario("adminfamilia@gmail.com");
        await _db.Hogares.AddAsync(new Hogar { NombreFamiliar = "Familia Rivera" });
        await _db.SaveChangesAsync();
        var hogar = _db.Hogares.Local.First();
        var invitacion = new InvitacionFamilia
        {
            IdHogar = hogar.IdHogar,
            IdInvitador = invitador.IdUsuario,
            IdInvitado = invitado.IdUsuario,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var email = new EmailStub();
        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@gmail.com", rol: "Miembro", email: email);
        var resultado = await controller.Aceptar(invitacion.IdInvitacion);

        Assert.IsType<OkObjectResult>(resultado.Result);
        // El alta también manda el correo de unión a los miembros del hogar;
        // aquí se verifica el resultado de la invitación para el invitador.
        var enviado = Assert.Single(email.Enviados.Where(e => e.Asunto == "resultado-invitacion"));
        Assert.Equal("adminfamilia@gmail.com", enviado.Destino);
        Assert.Equal("resultado-invitacion", enviado.Asunto);
    }

    [Fact]
    public async Task Rechazar_NotificaPorCorreoAlInvitador()
    {
        var invitado = await CrearUsuario("ana@gmail.com");
        var invitador = await CrearUsuario("adminfamilia@gmail.com");
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = invitador.IdUsuario,
            IdInvitado = invitado.IdUsuario,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        _db.InvitacionesFamilia.Add(invitacion);
        await _db.SaveChangesAsync();

        var email = new EmailStub();
        var controller = CrearInvitaciones(_db, invitado.IdUsuario, idHogar: 0, correo: "ana@gmail.com", rol: "Miembro", email: email);
        var resultado = await controller.Rechazar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("adminfamilia@gmail.com", enviado.Destino);
        Assert.Equal("resultado-invitacion", enviado.Asunto);
    }

    [Fact]
    public async Task Reenviar_VuelveAEnviarElCorreoDeInvitacion()
    {
        var invitacion = new InvitacionFamilia
        {
            IdHogar = 1,
            IdInvitador = 1,
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
            FechaEnvio = DateTime.UtcNow.AddDays(-5),
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await CrearUsuario("adminfamilia@gmail.com", idHogar: 1, rol: "Admin");
        await _db.Hogares.AddAsync(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Rivera" });
        await _db.SaveChangesAsync();

        var email = new EmailStub();
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com", rol: "Admin", email: email);
        var resultado = await controller.Reenviar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        // Antes del fix solo se actualizaba la fecha; ahora reenvía el correo.
        var enviado = Assert.Single(email.Enviados);
        Assert.Equal("ana@gmail.com", enviado.Destino);
        Assert.Equal("invitacion-familiar", enviado.Asunto);
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
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com", rol: "Admin");
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
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoAceptada,
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com", rol: "Admin");
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
            Correo = "ana@gmail.com",
            Estado = InvitacionFamilia.EstadoPendiente,
            FechaEnvio = DateTime.UtcNow.AddDays(-5),
        };
        await _db.InvitacionesFamilia.AddAsync(invitacion);
        await _db.SaveChangesAsync();

        var fechaAnterior = invitacion.FechaEnvio;
        var controller = CrearInvitaciones(_db, idUsuario: 1, idHogar: 1, correo: "admin@gmail.com", rol: "Admin");
        var resultado = await controller.Reenviar(invitacion.IdInvitacion);

        Assert.IsType<NoContentResult>(resultado);
        Assert.True(invitacion.FechaEnvio > fechaAnterior);
    }
}


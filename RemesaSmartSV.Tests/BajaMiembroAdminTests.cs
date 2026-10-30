using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas de la baja de miembro por parte de un Admin (DELETE /api/Usuarios/{id}):
/// el retiro es un detach —igual que abandonar familia— que conserva la fila
/// usuario y la autoría de sus movimientos, avisa por correo a los miembros
/// restantes y proyecta NombreAutor en GET /api/movimientos.
/// Ojo: EF InMemory no valida FK RESTRICT, así que estas pruebas fijan el camino
/// nuevo (detach) en lugar de simular el viejo 409 por movimientos.
/// </summary>
public class BajaMiembroAdminTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public BajaMiembroAdminTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

        // Jwt:Key de 64 bytes: HMAC-SHA256 exige como minimo 256 bits para firmar.
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "clave-de-prueba-remesasmart-0123456789-abcdefghij-0123456789",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            })
            .Build();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Stub de IEmailService que registra cada envío como (destino, asunto) para
    /// assertar el fan-out de la baja sin depender del SMTP real.
    /// </summary>
    private class EmailStub : IEmailService
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

    /// <summary>
    /// Espera a que el envío fire-and-forget del fan-out registre al menos
    /// <paramref name="minimo"/> correos antes de asertar, para no depender del
    /// orden del planificador de tareas.
    /// </summary>
    private static async Task<List<(string Destino, string Asunto)>> EsperarEnviosAsync(EmailStub email, int minimo)
    {
        for (var intento = 0; intento < 100 && email.Enviados.Count < minimo; intento++)
            await Task.Delay(10);
        return email.Enviados.ToList();
    }

    private UsuariosController CrearControllerUsuarios(EmailStub email, int idUsuario)
    {
        var controller = new UsuariosController(_db, new AuthService(_db, _config), email);
        AsignarClaims(controller, ("idUsuario", idUsuario.ToString()));
        return controller;
    }

    private MovimientosController CrearControllerMovimientos(int idHogar)
    {
        var controller = new MovimientosController(_db, new EmailStub());
        AsignarClaims(controller, ("idHogar", idHogar.ToString()));
        return controller;
    }

    private static void AsignarClaims(ControllerBase controller, params (string Tipo, string Valor)[] claims)
    {
        var lista = claims.Select(c => new Claim(c.Tipo, c.Valor)).ToList();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(lista, "Test")) }
        };
    }

    private Usuario SembrarMiembro(int idUsuario, int? idHogar, string correo, string nombre, string? rol = null)
    {
        var usuario = new Usuario
        {
            IdUsuario = idUsuario,
            IdHogar = idHogar,
            Nombre = nombre,
            Correo = correo,
            ContrasenaHash = "x",
            Rol = rol ?? (idHogar is null ? null : "Miembro"),
            FechaRegistro = DateTime.UtcNow,
        };
        _db.Usuarios.Add(usuario);
        return usuario;
    }

    /// <summary>Hogar "Familia Rivera" con un movimiento real del miembro dado.</summary>
    private async Task SembrarHogarAsync(params Usuario[] miembros)
    {
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Rivera" });
        _db.Usuarios.AddRange(miembros);
        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Comida", Tipo = "Gasto" });
        await _db.SaveChangesAsync();
    }

    private async Task SembrarMovimientoAsync(int idUsuario, int idMovimiento = 1)
    {
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = idMovimiento,
            IdHogar = 1,
            IdUsuario = idUsuario,
            IdCategoria = 1,
            Monto = 25.50m,
            Fecha = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            Tipo = "Gasto",
            Descripcion = "Compra del miembro removido"
        });
        await _db.SaveChangesAsync();
    }

    // ---------- B1: Delete como detach ----------

    [Fact]
    public async Task Delete_DetachaAlMiembro_YConservaSusMovimientos()
    {
        var admin = SembrarMiembro(1, 1, "admin@gmail.com", "Ada", rol: "Admin");
        var removido = SembrarMiembro(2, 1, "removido@gmail.com", "Beni");
        await SembrarHogarAsync(admin, removido);
        await SembrarMovimientoAsync(removido.IdUsuario);

        var controller = CrearControllerUsuarios(new EmailStub(), admin.IdUsuario);
        var result = await controller.Delete(removido.IdUsuario);

        Assert.IsType<NoContentResult>(result);

        // La fila usuario SOBREVIVE pero queda desvinculada del hogar.
        var usuario = await _db.Usuarios.FindAsync(removido.IdUsuario);
        Assert.NotNull(usuario);
        Assert.Null(usuario!.IdHogar);
        Assert.Null(usuario.Rol);
        Assert.Equal(removido.Correo, usuario.Correo);

        // Sus movimientos siguen existiendo y resolviendo contra la fila usuario.
        Assert.Single(await _db.Movimientos.Where(m => m.IdHogar == 1).ToListAsync());
        Assert.True(await _db.Movimientos.AnyAsync(m => m.IdUsuario == removido.IdUsuario));
    }

    [Fact]
    public async Task Delete_ConMovimientos_EnviaCorreoDeSalidaARestantes()
    {
        var admin = SembrarMiembro(1, 1, "admin@gmail.com", "Ada", rol: "Admin");
        var removido = SembrarMiembro(2, 1, "removido@gmail.com", "Beni");
        var restante = SembrarMiembro(3, 1, "restante@gmail.com", "Ceci");
        await SembrarHogarAsync(admin, removido, restante);
        await SembrarMovimientoAsync(removido.IdUsuario);

        var email = new EmailStub();
        var controller = CrearControllerUsuarios(email, admin.IdUsuario);
        var result = await controller.Delete(removido.IdUsuario);

        Assert.IsType<NoContentResult>(result);

        // El fan-out llega a TODOS los miembros restantes (incluido el admin
        // actor, consistente con self-leave) y NUNCA al miembro removido.
        var envios = await EsperarEnviosAsync(email, 2);
        var salidas = envios.Where(e => e.Asunto == "salida-familia").ToList();
        Assert.Equal(2, salidas.Count);
        Assert.Equal(
            new[] { "admin@gmail.com", "restante@gmail.com" },
            salidas.Select(e => e.Destino).OrderBy(d => d, StringComparer.Ordinal).ToList());
        Assert.DoesNotContain(envios, e => e.Destino == "removido@gmail.com");

        // El movimiento del removido queda en el hogar: es lo que "se entera"
        // la familia además del correo.
        Assert.True(await _db.Movimientos.AnyAsync(m => m.IdUsuario == removido.IdUsuario && m.IdHogar == 1));
    }

    [Fact]
    public async Task Delete_Self_BadRequest()
    {
        // El admin actor nunca puede ser el objetivo: el guard de self-delete
        // responde 400 aunque existan otros admins en el hogar.
        var admin = SembrarMiembro(1, 1, "admin@gmail.com", "Ada", rol: "Admin");
        var otroAdmin = SembrarMiembro(2, 1, "otro@gmail.com", "Beto", rol: "Admin");
        var miembro = SembrarMiembro(3, 1, "miembro@gmail.com", "Ceci");
        await SembrarHogarAsync(admin, otroAdmin, miembro);

        var email = new EmailStub();
        var controller = CrearControllerUsuarios(email, admin.IdUsuario);
        var result = await controller.Delete(admin.IdUsuario);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("propio usuario", bad.Value?.ToString());

        // Nadie fue desvinculado y no se envió ningún correo de salida.
        Assert.Equal(1, admin.IdHogar);
        Assert.Equal("Admin", admin.Rol);
        await Task.Delay(300);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task Delete_UltimoAdmin_BadRequest()
    {
        // El único Admin del hogar no puede ser retirado por DELETE. Como el
        // actor del request también debe ser Admin en el mismo hogar, la única
        // petición que dejaría a la familia sin Admin es la del último Admin
        // contra sí mismo: el guard de self-delete la intercepta con 400 (el
        // guard de último-admin queda cubierto detrás y no es alcanzable con un
        // actor Admin válido). La salida del último Admin es abandonar-familia.
        var admin = SembrarMiembro(1, 1, "admin@gmail.com", "Ada", rol: "Admin");
        var miembro = SembrarMiembro(2, 1, "miembro@gmail.com", "Ceci");
        await SembrarHogarAsync(admin, miembro);

        var email = new EmailStub();
        var controller = CrearControllerUsuarios(email, admin.IdUsuario);
        var result = await controller.Delete(admin.IdUsuario);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("propio usuario", bad.Value?.ToString());

        // La familia conserva a su único Admin y no recibe ningún aviso.
        Assert.Equal(1, admin.IdHogar);
        Assert.Equal("Admin", admin.Rol);
        await Task.Delay(300);
        Assert.Empty(email.Enviados);
    }

    // ---------- B2: autoría visible en GET /api/movimientos ----------

    [Fact]
    public async Task GetMovimientos_IncluyeNombreAutor_DeMiembroRemovido()
    {
        var admin = SembrarMiembro(1, 1, "admin@gmail.com", "Ada", rol: "Admin");
        // El removido ya fue detachado: la fila persiste con IdHogar = null.
        var removido = SembrarMiembro(2, null, "removido@gmail.com", "Beni", rol: null);
        await SembrarHogarAsync(admin, removido);
        await SembrarMovimientoAsync(removido.IdUsuario);

        var controller = CrearControllerMovimientos(idHogar: 1);
        var result = await controller.GetMovimientos(
            categoriaId: null, tipo: null, fechaInicio: null, fechaFin: null, usuarioId: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var pagina = Assert.IsType<PaginatedResponse<MovimientoListadoDto>>(ok.Value);
        var movimiento = Assert.Single(pagina.Items);

        // El DTO es superset: los campos del contrato crudo siguen intactos…
        Assert.Equal(removido.IdUsuario, movimiento.IdUsuario);
        Assert.Equal(1, movimiento.IdHogar);
        Assert.Equal("Gasto", movimiento.Tipo);
        Assert.Equal(25.50m, movimiento.Monto);

        // …y el nombre del autor removido se resuelve desde su fila usuario.
        Assert.Equal("Beni", movimiento.NombreAutor);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas del fan-out de correos de la familia cuando se registra un
/// movimiento compartido. Se verifican destinatarios y tipo de aviso con un
/// stub que captura cada envío como (Destino, Asunto).
/// </summary>
public class CorreosFamiliaTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public CorreosFamiliaTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

        // Jwt:Key de 64 bytes: HMAC-SHA256 exige como mínimo 256 bits para firmar.
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
    /// assertar el cableado de notificaciones sin depender del SMTP real.
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

    private static void AsignarClaims(ControllerBase controller, params (string Tipo, string Valor)[] claims)
    {
        var lista = claims.Select(c => new Claim(c.Tipo, c.Valor)).ToList();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(lista, "Test")) }
        };
    }

    // ---------- Movimientos registrados ----------

    [Theory]
    [InlineData("Gasto")]
    [InlineData("Ingreso")]
    [InlineData("Ahorro")]
    public async Task CrearMovimiento_ConHogar_EnviaUnCorreoPorMiembro(string tipo)
    {
        SembrarMiembro(1, 1, "autor@gmail.com", "Autor");
        SembrarMiembro(2, 1, "par@gmail.com", "Par");
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Rivera" });
        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Comida", Tipo = tipo });
        await _db.SaveChangesAsync();

        var email = new EmailStub();
        var controller = new MovimientosController(_db, email);
        AsignarClaims(controller, ("idUsuario", "1"), ("idHogar", "1"));

        var resultado = await controller.Create(new Movimiento
        {
            IdCategoria = 1,
            Monto = 18.75m,
            Fecha = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc),
            Tipo = tipo,
            Descripcion = "Compra de alimentos",
        });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);

        var envios = await EsperarEnviosAsync(email, 2);
        Assert.Equal(2, envios.Count);
        Assert.All(envios, e => Assert.Equal("movimiento-familia", e.Asunto));
        Assert.Equal(
            new[] { "autor@gmail.com", "par@gmail.com" },
            envios.Select(e => e.Destino).OrderBy(d => d, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task CrearMovimiento_EspejoDeAporte_NoEnviaCorreosDeMovimiento()
    {
        SembrarMiembro(1, 1, "autor@gmail.com", "Autor");
        SembrarMiembro(2, 1, "par@gmail.com", "Par");
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Rivera" });
        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Ahorro", Tipo = "Ahorro" });
        await _db.SaveChangesAsync();

        var email = new EmailStub();
        var controller = new MovimientosController(_db, email);
        AsignarClaims(controller, ("idUsuario", "1"), ("idHogar", "1"));

        var resultado = await controller.Create(new Movimiento
        {
            IdCategoria = 1,
            Monto = 50m,
            Fecha = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc),
            Tipo = "Ahorro",
            Descripcion = "Aporte a meta de ahorro",
            OrigenEmisora = "aporte:1",
        });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);

        // Se deja pasar el turno del fan-out: el espejo de un aporte no debe
        // generar ningún aviso de movimiento.
        await Task.Delay(300);
        Assert.Empty(email.Enviados);
    }
}

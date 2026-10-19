using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.Entities;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas de la regla de propiedad de <see cref="MovimientosController"/>:
/// solo el miembro que registró el movimiento o un administrador del hogar
/// pueden modificarlo o eliminarlo. Un miembro no dueño recibe 403; un
/// movimiento de otro hogar sigue devolviendo 404 para no revelar su existencia.
/// </summary>
public class MovimientosPropiedadTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    private class EmailStub : IEmailService
    {
        public Task<bool> EnviarInvitacionFamiliarAsync(string a, string b, string c, string d) => Task.FromResult(true);
        public Task<bool> EnviarResetContrasenaAsync(string a, string b) => Task.FromResult(true);
        public Task<bool> EnviarAlertaPresupuestoAsync(string a, string b, string c, decimal d, decimal e, decimal f) => Task.FromResult(true);
        public Task<bool> EnviarPresupuestoExcedidoAsync(string a, string b, string c, decimal d, decimal e) => Task.FromResult(true);
        public Task<bool> EnviarNotificacionCuentaAsync(string a, string b, string c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoAccesoAsync(string a, DateTime b, string? c) => Task.FromResult(true);
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string a, DateTime b) => Task.FromResult(true);
    }

    public MovimientosPropiedadTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Construye el controller con los claims del usuario autenticado.
    /// El authenticationType es obligatorio: sin él <c>User.IsInRole</c> no resuelve roles.
    /// </summary>
    private MovimientosController CrearController(int idUsuario, int idHogar, bool esAdmin)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString())
        };
        if (esAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var controller = new MovimientosController(_db, new EmailStub())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
        return controller;
    }

    /// <summary>
    /// Hogar 1 con un miembro dueño (usuario 1) y un administrador (usuario 2).
    /// El movimiento 1 pertenece al hogar 1 y fue registrado por el usuario 1.
    /// El movimiento 2 pertenece a otro hogar (2) para el caso 404.
    /// </summary>
    private void SembrarDatos()
    {
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Uno" });
        _db.Hogares.Add(new Hogar { IdHogar = 2, NombreFamiliar = "Familia Dos" });

        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            IdHogar = 1,
            Nombre = "Dueño",
            Correo = "dueno@gmail.com",
            ContrasenaHash = "x",
            Rol = "Miembro",
            FechaRegistro = DateTime.UtcNow
        });
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 2,
            IdHogar = 1,
            Nombre = "Admin",
            Correo = "admin@gmail.com",
            ContrasenaHash = "x",
            Rol = "Admin",
            FechaRegistro = DateTime.UtcNow
        });

        _db.Categorias.Add(new Categoria { IdCategoria = 1, IdHogar = 1, Nombre = "Comida", Tipo = "Gasto" });
        _db.Categorias.Add(new Categoria { IdCategoria = 2, IdHogar = 2, Nombre = "Transporte", Tipo = "Gasto" });

        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 1,
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = 1,
            Monto = 250m,
            Fecha = new DateTime(2026, 9, 15),
            Tipo = "Gasto",
            Descripcion = "Mercado original"
        });
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 2,
            IdHogar = 2,
            IdUsuario = 3,
            IdCategoria = 2,
            Monto = 50m,
            Fecha = new DateTime(2026, 9, 16),
            Tipo = "Gasto",
            Descripcion = "Ajeno a otro hogar"
        });
        // Movimiento espejo de un aporte (marca "aporte:{id}"): es de solo
        // lectura en Movimientos, se gestiona desde la página de Ahorro.
        _db.Movimientos.Add(new Movimiento
        {
            IdMovimiento = 3,
            IdHogar = 1,
            IdUsuario = 1,
            IdCategoria = 1,
            Monto = 50m,
            Fecha = new DateTime(2026, 9, 30),
            Tipo = "Ahorro",
            Descripcion = "Aporte a la meta Viaje",
            OrigenEmisora = "aporte:7"
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task Delete_DueñoDelMovimiento_ReturnsNoContent()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 1, idHogar: 1, esAdmin: false);

        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Movimientos.FindAsync(1));
    }

    [Fact]
    public async Task Delete_AdminDelHogar_BorraMovimientoDeOtroMiembro_ReturnsNoContent()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 2, idHogar: 1, esAdmin: true);

        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await _db.Movimientos.FindAsync(1));
    }

    [Fact]
    public async Task Delete_MiembroNoDueño_ReturnsForbiddenYConservaElMovimiento()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 2, idHogar: 1, esAdmin: false);

        var result = await controller.Delete(1);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Contains("eliminarlo", forbidden.Value?.ToString());

        // El movimiento ajeno sigue intacto en la base de datos.
        Assert.NotNull(await _db.Movimientos.FindAsync(1));
    }

    [Fact]
    public async Task Update_MiembroNoDueño_ReturnsForbiddenYNoModificaElRegistro()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 2, idHogar: 1, esAdmin: false);

        var input = new Movimiento
        {
            IdCategoria = 1,
            Monto = 999.99m,
            Fecha = new DateTime(2026, 10, 1),
            Tipo = "Gasto",
            Descripcion = "Intento de edición ajena"
        };

        var result = await controller.Update(1, input);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Contains("modificarlo", forbidden.Value?.ToString());

        // El registro no cambió: conserva monto y descripción originales.
        var movimiento = await _db.Movimientos.FindAsync(1);
        Assert.NotNull(movimiento);
        Assert.Equal(250m, movimiento!.Monto);
        Assert.Equal("Mercado original", movimiento.Descripcion);
    }

    [Fact]
    public async Task Update_DueñoDelMovimiento_ReturnsNoContent()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 1, idHogar: 1, esAdmin: false);

        var input = new Movimiento
        {
            IdCategoria = 1,
            Monto = 300m,
            Fecha = new DateTime(2026, 9, 20),
            Tipo = "Gasto",
            Descripcion = "Mercado editado por el dueño"
        };

        var result = await controller.Update(1, input);

        Assert.IsType<NoContentResult>(result);
        var movimiento = await _db.Movimientos.FindAsync(1);
        Assert.Equal(300m, movimiento!.Monto);
        Assert.Equal("Mercado editado por el dueño", movimiento.Descripcion);
    }

    [Fact]
    public async Task Delete_MovimientoDeOtroHogar_ReturnsNotFound()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 1, idHogar: 1, esAdmin: true);

        // El movimiento 2 es del hogar 2: se responde 404 (no 403) para no
        // revelar que existe en otro hogar.
        var result = await controller.Delete(2);

        Assert.IsType<NotFoundResult>(result);
        Assert.NotNull(await _db.Movimientos.FindAsync(2));
    }

    [Fact]
    public async Task Delete_MovimientoEspejoDeAporte_ReturnsConflictYLoConserva()
    {
        SembrarDatos();
        // El dueño del espejo (usuario 1) intenta borrarlo desde Movimientos.
        var controller = CrearController(idUsuario: 1, idHogar: 1, esAdmin: false);

        var result = await controller.Delete(3);

        var conflict = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Contains("aporte", conflict.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

        // El espejo sigue intacto: borrarlo dejaría el aporte huérfano.
        Assert.NotNull(await _db.Movimientos.FindAsync(3));
    }

    [Fact]
    public async Task Update_MovimientoEspejoDeAporte_ReturnsConflictYNoModificaElRegistro()
    {
        SembrarDatos();
        var controller = CrearController(idUsuario: 1, idHogar: 1, esAdmin: false);

        var input = new Movimiento
        {
            IdCategoria = 1,
            Monto = 999.99m,
            Fecha = new DateTime(2026, 10, 1),
            Tipo = "Gasto",
            Descripcion = "Intento de edición del espejo"
        };

        var result = await controller.Update(3, input);

        var conflict = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);

        var movimiento = await _db.Movimientos.FindAsync(3);
        Assert.NotNull(movimiento);
        Assert.Equal(50m, movimiento!.Monto);
        Assert.Equal("aporte:7", movimiento.OrigenEmisora);
    }
}

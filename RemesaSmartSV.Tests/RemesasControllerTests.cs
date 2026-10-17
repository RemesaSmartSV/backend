using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Controllers;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Cubre el ledger de remesas: registro con validación de montos y vínculos,
/// listado por año, permisos dueño/Admin sobre edición y borrado, y el
/// resumen anual con el desglose por canal.
/// </summary>
public class RemesasControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public RemesasControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Crea un controller con los claims del token. authenticationType es obligatorio:
    /// sin él la identidad queda sin autenticar y User.IsInRole() siempre devuelve false.
    /// </summary>
    private RemesasController CrearController(int idUsuario, int idHogar, bool esAdmin = false)
    {
        var claims = new List<Claim>
        {
            new("idUsuario", idUsuario.ToString()),
            new("idHogar", idHogar.ToString())
        };
        if (esAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var controller = new RemesasController(_db)
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

    /// <summary>Dos hogares: el 1 con dueño (usuario 1) y Admin (usuario 2); el 2 ajeno (usuario 3).</summary>
    private async Task SembrarHogaresAsync()
    {
        _db.Hogares.Add(new Hogar { IdHogar = 1, NombreFamiliar = "Familia Uno" });
        _db.Hogares.Add(new Hogar { IdHogar = 2, NombreFamiliar = "Familia Dos" });
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            IdHogar = 1,
            Nombre = "Dueña",
            Correo = "duena@gmail.com",
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
        _db.Usuarios.Add(new Usuario
        {
            IdUsuario = 3,
            IdHogar = 2,
            Nombre = "Ajeno",
            Correo = "ajeno@gmail.com",
            ContrasenaHash = "x",
            Rol = "Miembro",
            FechaRegistro = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static Remesa NuevaRemesa(
        int idHogar = 1,
        int? idUsuario = 1,
        string direccion = "Envio",
        string canal = "WesternUnion",
        decimal montoEnviado = 100m,
        decimal montoRecibido = 96m,
        decimal comision = 4m,
        DateTime? fecha = null)
        => new()
        {
            IdHogar = idHogar,
            IdUsuario = idUsuario,
            Direccion = direccion,
            Canal = canal,
            MontoEnviado = montoEnviado,
            MontoRecibido = montoRecibido,
            Comision = comision,
            Fecha = fecha ?? new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

    [Fact]
    public async Task Create_RemesaValida_Retorna201YAsignaHogarYUsuarioDelClaim()
    {
        await SembrarHogaresAsync();
        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Create(new Remesa
        {
            Direccion = "Envio",
            Canal = "WesternUnion",
            MontoEnviado = 100m,
            MontoRecibido = 96m,
            Comision = 4m,
            Folio = "MTCN1234567890",
            Notas = "Envío de prueba"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

        var guardada = await _db.Remesas.SingleAsync();
        Assert.Equal(1, guardada.IdHogar);
        Assert.Equal(1, guardada.IdUsuario);
        Assert.Equal("Envio", guardada.Direccion);
        Assert.Equal("WesternUnion", guardada.Canal);
        Assert.Equal(100m, guardada.MontoEnviado);
        Assert.Equal(96m, guardada.MontoRecibido);
        Assert.Equal(4m, guardada.Comision);
        Assert.Equal("MTCN1234567890", guardada.Folio);
        Assert.Equal("Envío de prueba", guardada.Notas);
    }

    [Fact]
    public async Task Create_MontoRecibidoMayorQueEnviado_Retorna400()
    {
        await SembrarHogaresAsync();
        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Create(new Remesa
        {
            Direccion = "Recepcion",
            Canal = "Giros",
            MontoEnviado = 100m,
            MontoRecibido = 150m,
            Comision = 0m
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        Assert.Contains("monto recibido", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_db.Remesas);
    }

    [Fact]
    public async Task Create_VinculadaDeOtroHogar_Retorna400()
    {
        await SembrarHogaresAsync();

        var ajena = NuevaRemesa(idHogar: 2, idUsuario: 3);
        _db.Remesas.Add(ajena);
        await _db.SaveChangesAsync();

        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Create(new Remesa
        {
            Direccion = "Recepcion",
            Canal = "Giros",
            MontoEnviado = 100m,
            MontoRecibido = 97m,
            Comision = 3m,
            IdRemesaVinculada = ajena.IdRemesa
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("vinculada", bad.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Solo quedó la remesa ajena; no se creó la del hogar 1.
        Assert.Single(_db.Remesas);
    }

    [Fact]
    public async Task GetRemesas_FiltradoPorAnio_SoloDevuelveEseAnio()
    {
        await SembrarHogaresAsync();

        _db.Remesas.Add(NuevaRemesa(canal: "WesternUnion", fecha: new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)));
        _db.Remesas.Add(NuevaRemesa(canal: "Giros", fecha: new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)));
        _db.Remesas.Add(NuevaRemesa(canal: "Efectivo", fecha: new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.GetRemesas(anio: 2026, direccion: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var remesas = Assert.IsType<List<Remesa>>(ok.Value);
        Assert.Equal(2, remesas.Count);
        Assert.All(remesas, r => Assert.Equal(2026, r.Fecha.Year));

        // Orden descendente por fecha: la de noviembre primero.
        Assert.Equal("Efectivo", remesas[0].Canal);
        Assert.Equal("Giros", remesas[1].Canal);
    }

    [Fact]
    public async Task Update_RemesaDeOtroMiembroComoMiembro_Retorna403()
    {
        await SembrarHogaresAsync();

        var ajena = NuevaRemesa(idUsuario: 2);
        _db.Remesas.Add(ajena);
        await _db.SaveChangesAsync();

        // Usuario 1 (Miembro) intenta editar la remesa del usuario 2.
        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Update(ajena.IdRemesa, new Remesa
        {
            Direccion = "Envio",
            Canal = "Otro",
            MontoEnviado = 50m,
            MontoRecibido = 45m,
            Comision = 5m
        });

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);

        // No se modificó nada.
        var enDb = await _db.Remesas.AsNoTracking().SingleAsync();
        Assert.Equal("WesternUnion", enDb.Canal);
    }

    [Fact]
    public async Task Update_RemesaDeOtroMiembroComoAdmin_Retorna204()
    {
        await SembrarHogaresAsync();

        var ajena = NuevaRemesa(idUsuario: 1);
        _db.Remesas.Add(ajena);
        await _db.SaveChangesAsync();

        // Usuario 2 es Admin del mismo hogar.
        var controller = CrearController(idUsuario: 2, idHogar: 1, esAdmin: true);

        var result = await controller.Update(ajena.IdRemesa, new Remesa
        {
            Direccion = "Recepcion",
            Canal = "Giros",
            MontoEnviado = 120m,
            MontoRecibido = 115m,
            Comision = 5m
        });

        Assert.IsType<NoContentResult>(result);

        var enDb = await _db.Remesas.AsNoTracking().SingleAsync();
        Assert.Equal("Recepcion", enDb.Direccion);
        Assert.Equal("Giros", enDb.Canal);
        Assert.Equal(120m, enDb.MontoEnviado);
        // El usuario que la registró no cambia.
        Assert.Equal(1, enDb.IdUsuario);
    }

    [Fact]
    public async Task Update_RemesaDeOtroHogar_Retorna404()
    {
        await SembrarHogaresAsync();

        var ajena = NuevaRemesa(idHogar: 2, idUsuario: 3);
        _db.Remesas.Add(ajena);
        await _db.SaveChangesAsync();

        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Update(ajena.IdRemesa, new Remesa
        {
            Direccion = "Envio",
            Canal = "Otro",
            MontoEnviado = 10m,
            MontoRecibido = 5m,
            Comision = 5m
        });

        Assert.IsType<NotFoundResult>(result);

        var enDb = await _db.Remesas.AsNoTracking().SingleAsync();
        Assert.Equal("WesternUnion", enDb.Canal);
    }

    [Fact]
    public async Task Delete_RemesaDeOtroHogar_Retorna404()
    {
        await SembrarHogaresAsync();

        var ajena = NuevaRemesa(idHogar: 2, idUsuario: 3);
        _db.Remesas.Add(ajena);
        await _db.SaveChangesAsync();

        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.Delete(ajena.IdRemesa);

        Assert.IsType<NotFoundResult>(result);
        Assert.Single(_db.Remesas);
    }

    [Fact]
    public async Task GetResumen_ConTresCanales_DevuelveTotalesYDesglose()
    {
        await SembrarHogaresAsync();

        _db.Remesas.Add(NuevaRemesa(canal: "WesternUnion", montoEnviado: 100m, montoRecibido: 96m, comision: 4m,
            fecha: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        _db.Remesas.Add(NuevaRemesa(canal: "Giros", montoEnviado: 200m, montoRecibido: 190m, comision: 10m,
            fecha: new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)));
        _db.Remesas.Add(NuevaRemesa(canal: "Efectivo", montoEnviado: 50m, montoRecibido: 50m, comision: 0m,
            fecha: new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)));
        // Fuera del año consultado: no debe sumar.
        _db.Remesas.Add(NuevaRemesa(canal: "WesternUnion", montoEnviado: 999m, montoRecibido: 999m, comision: 0m,
            fecha: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        var controller = CrearController(idUsuario: 1, idHogar: 1);

        var result = await controller.GetResumen(anio: 2026);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ResumenRemesasDTO>(ok.Value);

        Assert.Equal(2026, dto.Anio);
        Assert.Equal(3, dto.Cantidad);
        Assert.Equal(350m, dto.TotalEnviado);
        Assert.Equal(336m, dto.TotalRecibido);
        Assert.Equal(14m, dto.TotalComision);

        Assert.Equal(3, dto.PorCanal.Count);

        var efectivo = dto.PorCanal.Single(c => c.Canal == "Efectivo");
        Assert.Equal(1, efectivo.Cantidad);
        Assert.Equal(50m, efectivo.MontoEnviado);
        Assert.Equal(50m, efectivo.MontoRecibido);
        Assert.Equal(0m, efectivo.Comision);

        var giros = dto.PorCanal.Single(c => c.Canal == "Giros");
        Assert.Equal(1, giros.Cantidad);
        Assert.Equal(200m, giros.MontoEnviado);

        var western = dto.PorCanal.Single(c => c.Canal == "WesternUnion");
        Assert.Equal(1, western.Cantidad);
        Assert.Equal(100m, western.MontoEnviado);
    }
}

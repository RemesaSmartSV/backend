using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
/// Pruebas de la foto de perfil por archivo: guardado de bytes en Postgres,
/// servicio por streaming, borrado y validaciones de tipo/tamaño.
/// </summary>
public class FotoPerfilTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public FotoPerfilTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(options);

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

    private static void AsignarClaims(ControllerBase controller, int idUsuario)
    {
        var identity = new ClaimsIdentity(
            new List<Claim> { new("idUsuario", idUsuario.ToString()) }, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<(UsuariosController Controller, Usuario Usuario)> CrearUsuarioConControllerAsync()
    {
        var usuario = new Usuario
        {
            Nombre = "Ana",
            Correo = "ana@gmail.com",
            ContrasenaHash = "hash-de-prueba",
            Rol = "Miembro"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new UsuariosController(_db, new AuthService(_db, _config), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario);
        return (controller, usuario);
    }

    private static IFormFile Archivo(byte[] datos, string contentType, string nombre = "foto.png")
        => new ArchivoFalso(datos, contentType, nombre);

    [Fact]
    public async Task SubirFoto_PngValido_GuardaBytesContentTypeYRuta()
    {
        var (controller, usuario) = await CrearUsuarioConControllerAsync();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var result = await controller.SubirFoto(Archivo(bytes, "image/png"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var respuesta = Assert.IsType<FotoPerfilResponse>(ok.Value);
        Assert.Equal($"/api/Usuarios/foto/{usuario.IdUsuario}", respuesta.FotoPerfil);

        var guardado = await _db.Usuarios.SingleAsync();
        Assert.Equal(bytes, guardado.FotoArchivo);
        Assert.Equal("image/png", guardado.FotoContentType);
        Assert.Equal(respuesta.FotoPerfil, guardado.FotoPerfil);
    }

    [Fact]
    public async Task SubirFoto_JpegValido_NormalizaElContentType()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.SubirFoto(Archivo(new byte[] { 0xFF, 0xD8 }, "image/jpeg"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("image/jpeg", (await _db.Usuarios.SingleAsync()).FotoContentType);
    }

    [Fact]
    public async Task SubirFoto_SinArchivo_Retorna400()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.SubirFoto(null);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await _db.Usuarios.SingleAsync()).FotoArchivo);
    }

    [Fact]
    public async Task SubirFoto_ArchivoVacio_Retorna400()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.SubirFoto(Archivo(Array.Empty<byte>(), "image/png"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await _db.Usuarios.SingleAsync()).FotoArchivo);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("image/png; charset=binary")]
    public async Task SubirFoto_TipoNoPermitido_Retorna400(string contentType)
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.SubirFoto(Archivo(new byte[] { 1 }, contentType));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await _db.Usuarios.SingleAsync()).FotoArchivo);
    }

    [Fact]
    public async Task SubirFoto_SuperaCincoMegabytes_Retorna400()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();
        // El límite se valida por Length antes de leer el stream: no hace falta
        // materializar 6 MB solo para probar el rechazo.
        var grande = new ArchivoFalso(Array.Empty<byte>(), "image/png", "foto.png")
        {
            Length = 6 * 1024 * 1024
        };

        var result = await controller.SubirFoto(grande);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null((await _db.Usuarios.SingleAsync()).FotoArchivo);
    }

    [Fact]
    public async Task SubirFoto_UsuarioInexistente_Retorna404()
    {
        var usuario = new Usuario
        {
            Nombre = "Ana",
            Correo = "ana@gmail.com",
            ContrasenaHash = "hash-de-prueba"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new UsuariosController(_db, new AuthService(_db, _config), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario + 1);

        var result = await controller.SubirFoto(Archivo(new byte[] { 1 }, "image/png"));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SubirFoto_SegundaVez_ReemplazaLaFotoAnterior()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();
        await controller.SubirFoto(Archivo(new byte[] { 1, 2 }, "image/png"));

        var nueva = new byte[] { 9, 9, 9 };
        await controller.SubirFoto(Archivo(nueva, "image/jpeg"));

        var guardado = await _db.Usuarios.SingleAsync();
        Assert.Equal(nueva, guardado.FotoArchivo);
        Assert.Equal("image/jpeg", guardado.FotoContentType);
    }

    [Fact]
    public async Task ObtenerFoto_ConFoto_RetornaLosBytesConContentTypeYCaché()
    {
        var (controller, usuario) = await CrearUsuarioConControllerAsync();
        var bytes = new byte[] { 1, 2, 3, 4 };
        usuario.FotoArchivo = bytes;
        usuario.FotoContentType = "image/png";
        await _db.SaveChangesAsync();

        var result = await controller.ObtenerFoto(usuario.IdUsuario);

        var archivo = Assert.IsType<FileContentResult>(result);
        Assert.Equal(bytes, archivo.FileContents);
        Assert.Equal("image/png", archivo.ContentType);
        Assert.Equal("public, max-age=86400", controller.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task ObtenerFoto_SinFoto_Retorna404()
    {
        var (controller, usuario) = await CrearUsuarioConControllerAsync();

        var result = await controller.ObtenerFoto(usuario.IdUsuario);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ObtenerFoto_UsuarioInexistente_Retorna404()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.ObtenerFoto(id: 999999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ObtenerFoto_EsAnonimo()
    {
        // El <img> del navegador no lleva el header de autorización: sin esto
        // la avatar no cargaría en las listas de miembros.
        var metodo = typeof(UsuariosController)
            .GetMethod(nameof(UsuariosController.ObtenerFoto))!;

        Assert.NotNull(metodo.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task QuitarFoto_LimpiaBytesContentTypeYRuta()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();
        await controller.SubirFoto(Archivo(new byte[] { 1 }, "image/png"));

        var result = await controller.QuitarFoto();

        Assert.IsType<NoContentResult>(result);
        var limpio = await _db.Usuarios.SingleAsync();
        Assert.Null(limpio.FotoArchivo);
        Assert.Null(limpio.FotoContentType);
        Assert.Null(limpio.FotoPerfil);
    }

    [Fact]
    public async Task QuitarFoto_SinFoto_SigueRespondiendo204()
    {
        var (controller, _) = await CrearUsuarioConControllerAsync();

        var result = await controller.QuitarFoto();

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task QuitarFoto_UsuarioInexistente_Retorna404()
    {
        var usuario = new Usuario
        {
            Nombre = "Ana",
            Correo = "ana@gmail.com",
            ContrasenaHash = "hash-de-prueba"
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var controller = new UsuariosController(_db, new AuthService(_db, _config), new EmailRegistrador());
        AsignarClaims(controller, usuario.IdUsuario + 1);

        var result = await controller.QuitarFoto();

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Doble de IFormFile: evita depender de cómo FormFile resuelve ContentType
    /// (sale del header Content-Disposition) y permite declarar un Length mayor
    /// que los datos sin alocar el buffer completo.
    /// </summary>
    private sealed class ArchivoFalso : IFormFile
    {
        private readonly byte[] _datos;

        public ArchivoFalso(byte[] datos, string contentType, string fileName)
        {
            _datos = datos;
            ContentType = contentType;
            FileName = fileName;
            Length = datos.Length;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"archivo\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; set; }
        public string Name => "archivo";
        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_datos);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_datos);
            return Task.CompletedTask;
        }

        public Stream OpenReadStream() => new MemoryStream(_datos, writable: false);
    }

    /// <summary>Stub mínimo de IEmailService: la foto no dispara correos.</summary>
    private class EmailRegistrador : IEmailService
    {
        public Task<bool> EnviarInvitacionFamiliarAsync(string correoDestino, string nombreFamilia, string nombreInvitador, string token)
            => Task.FromResult(true);
        public Task<bool> EnviarResetContrasenaAsync(string correoDestino, string token)
            => Task.FromResult(true);
        public Task<bool> EnviarAlertaPresupuestoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado, decimal porcentaje)
            => Task.FromResult(true);
        public Task<bool> EnviarPresupuestoExcedidoAsync(string correoDestino, string nombreUsuario, string nombrePresupuesto, decimal montoLimite, decimal montoGastado)
            => Task.FromResult(true);
        public Task<bool> EnviarNotificacionCuentaAsync(string correoDestino, string asunto, string mensaje)
            => Task.FromResult(true);
        public Task<bool> EnviarAvisoAccesoAsync(string correoDestino, DateTime fechaUtc, string? origen)
            => Task.FromResult(true);
        public Task<bool> EnviarAvisoCambioContrasenaAsync(string correoDestino, DateTime fechaUtc)
            => Task.FromResult(true);
        public Task<bool> EnviarResultadoSolicitudUnionAsync(string correoDestino, string nombreSolicitante, string nombreFamilia, bool aceptada)
            => Task.FromResult(true);
        public Task<bool> EnviarResultadoInvitacionAsync(string correoDestino, string nombreFamilia, string nombreInvitado, bool aceptada)
            => Task.FromResult(true);
        public Task<bool> EnviarMovimientoFamiliaAsync(string correoDestino, string nombreDestino, string nombreAutor, string tipoMovimiento, decimal monto, string? descripcion, DateTime fecha)
            => Task.FromResult(true);
        public Task<bool> EnviarUnionFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
            => Task.FromResult(true);
        public Task<bool> EnviarSalidaFamiliaAsync(string correoDestino, string nombreFamilia, string nombrePersona)
            => Task.FromResult(true);
    }
}

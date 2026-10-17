using RemesaSmartSV.Entities;

namespace RemesaSmartSV.Tests;

/// <summary>
/// Pruebas del generador y validador de códigos de familia. El formato vigente
/// es un ID de 6 dígitos numéricos; el formato legacy "RS-FAM-" + 16
/// caracteres se sigue aceptando en la validación para no romper hogares ya
/// emitidos.
/// </summary>
public class CodigoHogarTests
{
    [Fact]
    public void Generar_ProduceSeisDigitosAscii()
    {
        for (var i = 0; i < 100; i++)
        {
            var codigo = CodigoHogar.Generar();

            Assert.Equal(6, codigo.Length);
            Assert.All(codigo, c => Assert.InRange(c, '0', '9'));
            Assert.True(CodigoHogar.EsValido(codigo));
        }
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("048293")]
    [InlineData("RS-FAM-ABCDEFGHJKLMNPQR")]  // legacy con el alfabeto del generador anterior
    [InlineData("RS-FAM-83656A6F88F54631")]  // legacy hex histórico presente en la BD
    [InlineData("rs-fam-abcdefghjklmnpqr")]  // legacy en minúsculas: Normalizar sube a mayúsculas
    [InlineData(" 123456 ")]                 // el trim/espacios de Normalizar siguen aplicando
    public void EsValido_AceptaFormatosValidos(string codigo)
        => Assert.True(CodigoHogar.EsValido(codigo));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]                 // 5 dígitos: muy corto
    [InlineData("1234567")]               // 7 dígitos: muy largo
    [InlineData("12345A")]                // carácter no numérico
    [InlineData("RS-FAM- corto")]         // legacy con muy pocos caracteres tras el prefijo
    [InlineData("RS-FAM-83656A6F88F546")] // legacy de 15 caracteres: longitud incorrecta
    [InlineData("0482-93")]               // separador no permitido en el formato vigente
    public void EsValido_RechazaFormatosInvalidos(string codigo)
        => Assert.False(CodigoHogar.EsValido(codigo));

    [Fact]
    public void EsValido_RechazaNull()
        => Assert.False(CodigoHogar.EsValido(null!));
}

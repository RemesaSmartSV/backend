using RemesaSmartSV.DTOs;

namespace RemesaSmartSV.Tests;

public class AlertasDtosTests
{
    [Fact]
    public void AlertaPeriodoRequestDTO_FechasRequeridas()
    {
        var dto = new AlertaPeriodoRequestDTO
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 31)
        };

        Assert.Equal(new DateTime(2026, 1, 1), dto.FechaInicio);
        Assert.Equal(new DateTime(2026, 1, 31), dto.FechaFin);
    }

    [Fact]
    public void AlertaResponseDTO_CreaInstancia()
    {
        var dto = new AlertaResponseDTO("Crítico", "Límite superado", 120.5m);

        Assert.Equal("Crítico", dto.TipoAlerta);
        Assert.Equal("Límite superado", dto.Mensaje);
        Assert.Equal(120.5m, dto.PorcentajeUsado);
    }
}

public class ReportesDtosTests
{
    [Fact]
    public void ReportePeriodoRequestDTO_FechasRequeridas()
    {
        var dto = new ReportePeriodoRequestDTO
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 31)
        };

        Assert.Equal(new DateTime(2026, 1, 1), dto.FechaInicio);
        Assert.Equal(new DateTime(2026, 1, 31), dto.FechaFin);
    }

    [Fact]
    public void ReportePeriodoResponseDTO_CreaInstancia()
    {
        var dto = new ReportePeriodoResponseDTO(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31),
            1000m,
            500m,
            500m,
            5,
            3);

        Assert.Equal(1000m, dto.TotalIngresos);
        Assert.Equal(500m, dto.TotalGastos);
        Assert.Equal(500m, dto.Balance);
        Assert.Equal(5, dto.CantidadIngresos);
        Assert.Equal(3, dto.CantidadGastos);
    }
}

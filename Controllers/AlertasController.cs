using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AlertasController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("periodo")]
    public async Task<ActionResult<IEnumerable<AlertaResponseDTO>>> GetAlertasPorPeriodo([FromQuery] AlertaPeriodoRequestDTO request)
    {
        if (request.FechaInicio > request.FechaFin)
        {
            return BadRequest(new { message = "La fecha de inicio no puede ser mayor a la fecha de fin." });
        }

        var idHogar = User.GetIdHogar();
        var fechaInicio = DateTime.SpecifyKind(request.FechaInicio.Date, DateTimeKind.Utc);
        var fechaFinExclusiva = DateTime.SpecifyKind(request.FechaFin.Date.AddDays(1), DateTimeKind.Utc);
        var periodoInicio = new DateTime(fechaInicio.Year, fechaInicio.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodoFinExclusiva = new DateTime(fechaFinExclusiva.Year, fechaFinExclusiva.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        var presupuestos = await _context.Presupuestos
            .AsNoTracking()
            .Where(p => p.IdHogar == idHogar
                     && p.MesAnio >= periodoInicio
                     && p.MesAnio < periodoFinExclusiva)
            .Select(p => new
            {
                p.IdCategoria,
                p.MontoLimite,
                CategoriaNombre = p.Categoria.Nombre
            })
            .ToListAsync();

        if (!presupuestos.Any())
        {
            return Ok(new List<AlertaResponseDTO>());
        }

        var fechaInicio = DateTime.SpecifyKind(request.FechaInicio.Date, DateTimeKind.Utc);
        var fechaFinExclusiva = DateTime.SpecifyKind(request.FechaFin.Date.AddDays(1), DateTimeKind.Utc);

        // Los movimientos también se consultan por hogar, igual que en MovimientosController.
        var gastosPeriodo = await _context.Movimientos
            .AsNoTracking()
            .Where(m => m.IdHogar == idHogar
                     && m.Tipo == "Gasto"
                     && m.Fecha >= fechaInicio
                     && m.Fecha < fechaFinExclusiva)
            .GroupBy(m => m.IdCategoria)
            .Select(g => new { IdCategoria = g.Key, TotalGastado = g.Sum(m => m.Monto) })
            .ToDictionaryAsync(g => g.IdCategoria, g => g.TotalGastado);

        var alertas = new List<AlertaResponseDTO>();

        foreach (var presupuesto in presupuestos)
        {
            var totalGastado = gastosPorCategoria.GetValueOrDefault(presupuesto.IdCategoria);

            if (presupuesto.MontoLimite > 0)
            {
                var porcentajeUsado = (totalGastado / presupuesto.MontoLimite) * 100;

                if (porcentajeUsado >= 100)
                {
                    alertas.Add(new AlertaResponseDTO(
                        "Crítico", 
                        $"Has superado tu límite en {presupuesto.CategoriaNombre}. Límite: ${presupuesto.MontoLimite}, Gastado: ${totalGastado}",
                        Math.Round(porcentajeUsado, 2)
                    ));
                }
                else if (porcentajeUsado >= 80)
                {
                    alertas.Add(new AlertaResponseDTO(
                        "Advertencia", 
                        $"Estás a punto de superar tu presupuesto en {presupuesto.CategoriaNombre}. Límite: ${presupuesto.MontoLimite}, Gastado: ${totalGastado}",
                        Math.Round(porcentajeUsado, 2)
                    ));
                }
            }
        }

        return Ok(alertas);
    }
}

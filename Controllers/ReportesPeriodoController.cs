using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemesaSmartSV.Data;
using RemesaSmartSV.DTOs;
using RemesaSmartSV.Services;

namespace RemesaSmartSV.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesPeriodoController : ControllerBase
{
	private readonly ApplicationDbContext _db;

	public ReportesPeriodoController(ApplicationDbContext db) => _db = db;

	[HttpGet]
	public async Task<ActionResult<ReportePeriodoResponseDTO>> GetReportePorPeriodo(
		[FromQuery] ReportePeriodoRequestDTO request)
	{
		if (request.FechaInicio > request.FechaFin)
			return BadRequest(new { message = "La fecha de inicio no puede ser mayor a la fecha de fin." });

		var fechaInicio = request.FechaInicio.Date;
		var fechaFinExclusiva = request.FechaFin.Date.AddDays(1);
		var idHogar = User.GetIdHogar();

		var resumen = await _db.Movimientos
			.AsNoTracking()
			.Where(m => m.IdHogar == idHogar
					 && m.Fecha >= fechaInicio
					 && m.Fecha < fechaFinExclusiva)
			.GroupBy(_ => 1)
			.Select(grupo => new
			{
				TotalIngresos = grupo
					.Where(m => m.Tipo == "Ingreso")
					.Sum(m => (decimal?)m.Monto) ?? 0m,
				TotalGastos = grupo
					.Where(m => m.Tipo == "Gasto")
					.Sum(m => (decimal?)m.Monto) ?? 0m,
				CantidadIngresos = grupo.Count(m => m.Tipo == "Ingreso"),
				CantidadGastos = grupo.Count(m => m.Tipo == "Gasto")
			})
			.SingleOrDefaultAsync();

		var totalIngresos = resumen?.TotalIngresos ?? 0m;
		var totalGastos = resumen?.TotalGastos ?? 0m;

		return Ok(new ReportePeriodoResponseDTO(
			fechaInicio,
			request.FechaFin.Date,
			totalIngresos,
			totalGastos,
			totalIngresos - totalGastos,
			resumen?.CantidadIngresos ?? 0,
			resumen?.CantidadGastos ?? 0));
	}
}

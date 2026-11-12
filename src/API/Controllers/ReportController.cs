using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Reports;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Models;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportController(ReportService reportService, IReportExcelExporter excelExporter) : ControllerBase
{
    // Tipos de reporte disponibles para el rol del token.
    [HttpGet("types")]
    public IActionResult GetTypes() => Ok(reportService.GetTypes(GetUserRole()));

    // Vista previa (máx. 50 filas). 
    [HttpGet("{type}/preview")]
    public async Task<IActionResult> Preview(
        string type,
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate,
        [FromQuery] ReservationStatus? status,
        [FromQuery] Guid? spaceId,
        [FromQuery] Guid? resourceId,
        [FromQuery(Name = "userId")] Guid? requesterId,
        [FromQuery] Guid? careerId,
        CancellationToken ct)
    {
        var filter = new ReportFilter(fromDate, toDate, status, spaceId, resourceId, requesterId, careerId);

        var result = await reportService.GetPreviewAsync(type, filter, GetUserId(), GetUserRole(), ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    // Descarga el reporte completo (sin límite de 50) en .xlsx.
    [HttpGet("{type}/export")]
    public async Task<IActionResult> Export(
        string type,
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate,
        [FromQuery] ReservationStatus? status,
        [FromQuery] Guid? spaceId,
        [FromQuery] Guid? resourceId,
        [FromQuery(Name = "userId")] Guid? requesterId,
        [FromQuery] Guid? careerId,
        CancellationToken ct)
    {
        var filter = new ReportFilter(fromDate, toDate, status, spaceId, resourceId, requesterId, careerId);
        var role = GetUserRole();

        var result = await reportService.GetAllAsync(type, filter, GetUserId(), role, ct);
        if (result.IsFailure)
            return ErrorResult(result.Error);

        var name = reportService.GetTypes(role).First(t => t.Code == type).Name;

        var nowUtc = DateTime.UtcNow;
        var bytes = excelExporter.Export(name, filter, result.Value, nowUtc);

        var fileName = $"reporte-{type}-{nowUtc.AddHours(-5):yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private IActionResult ErrorResult(Error error)
    {
        var body = new { error.Code, error.Description };

        if (error.Code == "Forbidden") return StatusCode(403, body);
        if (error.Code.EndsWith(".NotFound")) return NotFound(body);
        return BadRequest(body);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private RoleCode GetUserRole() =>
        Enum.Parse<RoleCode>(User.FindFirstValue(ClaimTypes.Role)!);
}
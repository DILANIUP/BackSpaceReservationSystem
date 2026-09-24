using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Alert;

namespace SpaceReservationSystem.API.Controllers;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertController : ControllerBase
{
    private readonly AlertService _alertService;

    public AlertController(AlertService alertService) => _alertService = alertService;

    // consultar una alerta
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _alertService.GetByIdAsync(id, ct);

        if (result.IsFailure)
            return NotFound(result.Error);

        var response = new AlertResponse(
            result.Value.Id,
            result.Value.Type,
            result.Value.Description,
            result.Value.ResolvedAt,
            result.Value.IsResolved,
            result.Value.ResolutionObservation,
            result.Value.ResourceId,
            result.Value.SpaceId);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var alerts = await _alertService.GetAllAsync(ct);

        var response = alerts.Select(a => new AlertResponse(
            a.Id,
            a.Type,
            a.Description,
            a.ResolvedAt,
            a.IsResolved,
            a.ResolutionObservation,
            a.ResourceId,
            a.SpaceId));

        return Ok(response);
    }

    // reportar una nueva incidencia (daño, mantenimiento)
    [HttpPost]
    [Authorize(Roles = "Student,Teacher,Bienes,Admin")]
    public async Task<IActionResult> Create(CreateAlertRequest request, CancellationToken ct)
    {
        var result = await _alertService.CreateAsync(
            request.Type, request.Description, request.ResourceId, request.SpaceId, ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        var response = new AlertResponse(
            result.Value.Id,
            result.Value.Type,
            result.Value.Description,
            result.Value.ResolvedAt,
            result.Value.IsResolved,
            result.Value.ResolutionObservation,
            result.Value.ResourceId,
            result.Value.SpaceId);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, response);
    }

    // marcar la incidencia como resuelta
    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Roles = "Bienes,Admin")]
    public async Task<IActionResult> Resolve(Guid id, ResolveAlertRequest request, CancellationToken ct)
    {
        var result = await _alertService.ResolveAsync(id, request.Observation, ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return NoContent();
    }
}
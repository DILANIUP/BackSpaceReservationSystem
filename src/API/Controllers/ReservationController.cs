using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Reservations;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationController(ReservationService reservationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest request, CancellationToken ct)
    {
        var result = await reservationService.CreateAsync(request, GetUserId(), GetUserRole(), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var reservations = await reservationService.GetMineAsync(GetUserId(), ct);
        return Ok(reservations);
    }

    // Consulta la disponibilidad real de los recursos para una fecha y horario
    [HttpGet("resource-availability")]
    public async Task<IActionResult> GetResourceAvailability(
        [FromQuery] DateTime date,
        [FromQuery] TimeSpan startTime,
        [FromQuery] TimeSpan endTime,
        CancellationToken ct)
    {
        var result = await reservationService.GetResourceAvailabilityAsync(
            date,
            startTime,
            endTime,
            ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await reservationService.GetByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Description });
    }

    [HttpGet("career")]
    [Authorize(Roles = "Coordinator, Admin")]
    public async Task<IActionResult> GetByCareer(CancellationToken ct)
    {
        var result = await reservationService.GetByCareerAsync(GetUserId(), GetUserRole(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }



    [HttpGet("vicerrector")]
    [Authorize(Roles = "Vicerrector, Admin")]
    public async Task<IActionResult> GetForVicerrector(CancellationToken ct)
    {
        var result = await reservationService.GetForVicerrectorAsync(ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpGet("assets")]
    [Authorize(Roles = "Bienes, Admin")]
    public async Task<IActionResult> GetForAssets(CancellationToken ct)
    {
        var result = await reservationService.GetForAssetsAsync(ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Student,Teacher,Coordinator,Vicerrector,Bienes,Admin")]
    public Task<IActionResult> Submit(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
    => Handle(reservationService.SubmitAsync(id, GetUserId(), GetUserRole(), request.Justification, ct));

    [HttpPost("{id:guid}/elevate")]
    [Authorize(Roles = "Coordinator,Admin")]
    public Task<IActionResult> Elevate(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
        => Handle(reservationService.ElevateToVicerrectorAsync(id, GetUserId(), request.Justification, ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Vicerrector,Admin")]
    public Task<IActionResult> Approve(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
        => Handle(reservationService.ApproveByVicerrectorAsync(id, GetUserId(), request.Justification, ct));

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = "Bienes,Admin")]
    public Task<IActionResult> Assign(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
        => Handle(reservationService.AssignBySpaceManagementAsync(id, GetUserId(), request.Justification, request.SpaceId, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Coordinator,Vicerrector,Bienes,Admin")]
    public Task<IActionResult> Reject(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
        => Handle(reservationService.RejectAsync(id, GetUserId(), GetUserRole(), request.Justification, ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Student,Teacher,Admin")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
        => Handle(reservationService.CancelAsync(id, GetUserId(), request.Justification, ct));

    private async Task<IActionResult> Handle(Task<Result<ReservationResponse>> operation)
    {
        var result = await operation;
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditReservationRequest request, CancellationToken ct)
    {
        var result = await reservationService.EditAsync(id, request, GetUserId(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private RoleCode GetUserRole() =>
        Enum.Parse<RoleCode>(User.FindFirstValue(ClaimTypes.Role)!);
}
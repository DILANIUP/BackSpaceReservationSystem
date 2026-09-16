using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Career;

namespace SpaceReservationSystem.API.Controllers;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CareerController : ControllerBase
{
    private readonly CareerService _careerService;

    public CareerController(CareerService careerService)
    {
        _careerService = careerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var careers = await _careerService.GetAllAsync(ct);
        var response = careers.Select(c => new CareerResponse(c.Id, c.Name, c.FacultyId));
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var result = await _careerService.GetByIdAsync(id, ct);

        if (result.IsFailure)
            return NotFound(result.Error);

        var response = new CareerResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.FacultyId);

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        CreateCareerRequest request,
        CancellationToken ct)
    {
        var result = await _careerService.CreateAsync(
            request.Name,
            request.FacultyId,
            ct
        );

        if (result.IsFailure)
            return BadRequest(result.Error);

        var response = new CareerResponse(
            result.Value.Id,
            result.Value.Name,
            result.Value.FacultyId
        );

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            response
        );
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateCareerRequest request,
        CancellationToken ct)
    {
        var result = await _careerService.UpdateAsync(
            id,
            request.Name,
            ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return NoContent();
    }
}
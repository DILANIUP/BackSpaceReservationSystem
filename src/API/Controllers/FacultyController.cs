using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Faculty;

namespace SpaceReservationSystem.API.Controllers;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class FacultyController : ControllerBase
{
    private readonly FacultyService _facultyService;

    public FacultyController(FacultyService facultyService)
    {
        _facultyService = facultyService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var faculties = await _facultyService.GetAllAsync(ct);
        var response = faculties.Select(f => new FacultyResponse(f.Id, f.Name));
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var result = await _facultyService.GetByIdAsync(id, ct);

        if (result.IsFailure)
            return NotFound(result.Error);

        var response = new FacultyResponse(
            result.Value.Id,
            result.Value.Name);

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        CreateFacultyRequest request,
        CancellationToken ct)
    {
        var result = await _facultyService.CreateAsync(
            request.Name,
            ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        var response = new FacultyResponse(
            result.Value.Id,
            result.Value.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateFacultyRequest request,
        CancellationToken ct)
    {
        var result = await _facultyService.UpdateAsync(
            id,
            request.Name,
            ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return NoContent();
    }
}
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Auth;
using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Application.Features.Users;
using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserController(UserService userService, AuthService authService) : ControllerBase
{
    [HttpPatch("{id:guid}/career")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignCareer(Guid id, [FromBody] AssingCareerRequest request, CancellationToken ct)
    {
        var result = await userService.AssignCareerAsync(id, request.CareerId, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpGet]
    [Authorize(Roles = "Coordinator,Vicerrector,Bienes,Admin")]
    public async Task<IActionResult> Search([FromQuery] RoleCode? role, [FromQuery] Guid? careerId, CancellationToken ct)
    {
        var users = await userService.SearchAsync(role, careerId, ct);
        return Ok(users);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await userService.GetMeAsync(GetUserId(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Description });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    // NUEVO: el Admin crea usuarios con el rol que elija. 
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.CreateByAdminAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct)
    {
        var result = await userService.SetStatusAsync(id, request.IsActive, GetUserId(), ct);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EditUserRequest request, CancellationToken ct)
    {
        var result = await userService.UpdateAsync(id, request, GetUserId(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Description });
    }
    [HttpGet("manage")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage(
        [FromQuery] RoleCode? role,
        [FromQuery] Guid? careerId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var users = await userService.SearchForAdminAsync(role, careerId, search, ct);
        return Ok(users);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Application.Features.Users;
using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserController(UserService userService) : ControllerBase
{
    [HttpPatch("{id:guid}/career")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignCareer(Guid id, [FromBody] AssingCarrerRequest request, CancellationToken ct)
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
}
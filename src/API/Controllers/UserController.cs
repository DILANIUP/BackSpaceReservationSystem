using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Application.Features.Users;

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
}
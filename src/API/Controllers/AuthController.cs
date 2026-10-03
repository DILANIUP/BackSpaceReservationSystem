using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Auth;
using SpaceReservationSystem.Domain.Errors;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(Register), new { id = result.Value.UserId }, result.Value);

        var error = new { result.Error.Code, result.Error.Description };
        return result.Error.Code == UserErrors.EmailAlreadyExists.Code
            ? Conflict(error)      // 409
            : BadRequest(error);   // 400
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new {result.Error.Code, result.Error.Description});
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Notification;
using SpaceReservationSystem.Domain.Interfaces;

namespace SpaceReservationSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationController(
        NotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        CancellationToken ct)
    {
        if (!_currentUserService.UserId.HasValue)
            return Unauthorized();

        var notifications = await _notificationService.GetByUserIdAsync(
            _currentUserService.UserId.Value,
            ct);

        return Ok(notifications);
    }
}
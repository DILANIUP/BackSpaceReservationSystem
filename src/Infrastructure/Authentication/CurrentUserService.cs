using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SpaceReservationSystem.Domain.Interfaces;

namespace SpaceReservationSystem.Infrastructure.Authentication;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var userId = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(userId, out var id)
                ? id
                : null;
        }
    }
}
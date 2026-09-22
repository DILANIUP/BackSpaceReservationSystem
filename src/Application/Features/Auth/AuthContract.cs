using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.Application.Features.Auth;

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string Phone,
    RoleCode RequestedRole = RoleCode.Student,
    Guid? CareerId = null
);

public sealed record RegisterResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken
);

public sealed record LoginRequest(
    string Email,
    string Password
);

public sealed record LoginResponse(
    Guid Userid,
    string AccessToken,
    string RefreshToken
);
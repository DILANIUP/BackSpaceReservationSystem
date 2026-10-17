using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.Application.Features.User;

public sealed record AssingCareerRequest(
    Guid CareerId
);

public sealed record UserSummaryResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? CareerId
);

public sealed record EditUserRequest(
    string Name,
    string LastName,
    string Phone,
    RoleCode Role,
    Guid? CareerId
);

public sealed record UserListItemResponse(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Role,
    Guid? CareerId,
    string? CareerName,
    bool IsActive
);

// NUEVO: PATCH para activar o desactivar una cuenta.
public sealed record SetStatusRequest(bool IsActive);
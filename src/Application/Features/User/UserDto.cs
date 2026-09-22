namespace SpaceReservationSystem.Application.Features.User;

public sealed record AssingCarrerRequest(
    Guid CareerId
);

public sealed record UserSummaryResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? CareerId
);
namespace SpaceReservationSystem.Application.Features.Reservations;

public sealed record CreateReservationRequest(
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    Guid? SpaceId,
    List<ReservationResourceRequest>? Resources = null
);

public sealed record ReservationResponse(
    Guid Id,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    string CurrentStatus,
    Guid UserId,
    Guid? SpaceId,
    string? RequesterName = null,
    string? RequesterRole = null,
    string? SpaceName = null
);

public sealed record TransitionRequest(
    string Justification,
    Guid? SpaceId = null
);

public sealed record ReservationResourceRequest(
    Guid ResourceId,
    int Quantity
);
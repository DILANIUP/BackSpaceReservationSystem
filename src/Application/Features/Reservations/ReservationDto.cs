namespace SpaceReservationSystem.Application.Features.Reservations;

public sealed record CreateReservationRequest(
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    Guid? SpaceId,
    List<ReservationResourceRequest>? Resources = null,
    Guid? OnBehalfOfUserId = null
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
    string? SpaceName = null,
    string? CareerName = null
);

public sealed record ReservationDetailResponse (
    Guid Id,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    string CurrentStatus,
    Guid UserId,
    Guid? SpaceId,
    string? RequesterName,
    string? RequesterRole,
    string? SpaceName,
    string? CareerName,
    List<ReservationResourceItemResponse> Resources,
    List<ReservationHistoryEntryResponse> History
);

public sealed record ReservationResourceItemResponse(
    string ResourceName,
    int Quantity
);

public sealed record ReservationHistoryEntryResponse(
    string NewStatus,
    string Justification,
    DateTime ChangeDate,
    string ChangedByName,
    string? ChangedByRole
);

public sealed record TransitionRequest(
    string Justification,
    Guid? SpaceId = null
);

public sealed record ReservationResourceRequest(
    Guid ResourceId,
    int Quantity
);

public sealed record EditReservationRequest(
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    Guid? SpaceId,
    List<ReservationResourceRequest>? Resources = null
);
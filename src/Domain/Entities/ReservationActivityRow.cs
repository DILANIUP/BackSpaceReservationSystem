using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.Domain.Models;
public sealed record ReservationActivityRow(
    Guid ReservationId,
    string? SpaceName,
    ReservationStatus NewStatus,
    string ChangedByName,
    string? ChangedByLastName,
    string? ChangedByRole,
    DateTime ChangeDate,
    string? Justification
);
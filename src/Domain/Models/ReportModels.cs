using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.Domain.Models;
public enum ReportKind
{
    MyReservations,
    CoordinatorHandled,
    CoordinatorAssisted,
    VicerrectorHandled,
    AssetsAssigned,
    AdminAll
}

// Filtros 
public sealed record ReportFilter(
    DateOnly? From,
    DateOnly? To,
    ReservationStatus? Status,
    Guid? SpaceId,
    Guid? ResourceId,
    Guid? UserId,    // solicitante
    Guid? CareerId   // carrera del solicitante
);

public sealed record ReportResourceItem(string Name, int Quantity);

// Fila tal como sale de la base
public sealed record ReportSourceRow(
    Guid Id,
    string RequesterName,
    string? RequesterLastName,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? SpaceName,
    string Reason,
    ReservationStatus Status,
    DateTime RequestDate,
    List<ReportResourceItem> Resources
);
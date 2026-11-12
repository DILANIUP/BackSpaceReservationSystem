using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Models;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _context;

    public ReportRepository(AppDbContext context) => _context = context;

    public async Task<int> CountAsync(ReportKind kind, Guid actingUserId, ReportFilter filter, CancellationToken ct = default)
        => await BuildQuery(kind, actingUserId, filter).CountAsync(ct);

    public async Task<List<ReportSourceRow>> GetRowsAsync(
        ReportKind kind, Guid actingUserId, ReportFilter filter, int? limit, CancellationToken ct = default)
    {
        IQueryable<Reservation> query = BuildQuery(kind, actingUserId, filter)
            .OrderByDescending(r => r.Slot.Date)
            .ThenByDescending(r => r.Slot.StartTime)
            .ThenBy(r => r.Id);

        if (limit is { } n)
            query = query.Take(n);

        return await query
            .Select(r => new ReportSourceRow(
                r.Id,
                r.User.Name,
                r.User.LastName,
                r.Slot.Date,
                r.Slot.StartTime,
                r.Slot.EndTime,
                r.Space != null ? r.Space.Name : null, // reserva solo de recursos: sin espacio
                r.Reason,
                r.CurrentStatus,
                r.RequestDate,
                r.ReservationResources
                    .OrderBy(rr => rr.Resource.Name)
                    .Select(rr => new ReportResourceItem(rr.Resource.Name, rr.RequestedQuantity))
                    .ToList()))
            .ToListAsync(ct);
    }

    // 1) ALCANCE (qué reservas puede ver ese tipo de reporte) y 2) FILTROS opcionales.
    // El alcance va primero: los filtros solo pueden REDUCIR el resultado, nunca ampliarlo.
    private IQueryable<Reservation> BuildQuery(ReportKind kind, Guid me, ReportFilter f)
    {
        var query = _context.Reservations.AsNoTracking();

        query = kind switch
        {
            ReportKind.MyReservations => query.Where(r => r.UserId == me),

            ReportKind.CoordinatorAssisted => query.Where(r => r.CreatedBy == me && r.UserId != me),

    
            ReportKind.CoordinatorHandled => query.Where(r => r.ReservationHistories.Any(h =>
                h.ChangedById == me
                && h.PreviousStatus == ReservationStatus.PendingCoordinator
                && (h.NewStatus == ReservationStatus.PendingVicerrector || h.NewStatus == ReservationStatus.Draft))),

            ReportKind.VicerrectorHandled => query.Where(r => r.ReservationHistories.Any(h =>
                h.ChangedById == me
                && h.PreviousStatus == ReservationStatus.PendingVicerrector
                && (h.NewStatus == ReservationStatus.PendingAssets || h.NewStatus == ReservationStatus.Rejected))),

            ReportKind.AssetsAssigned => query.Where(r => r.ReservationHistories.Any(h =>
                h.ChangedById == me
                && h.PreviousStatus == ReservationStatus.PendingAssets
                && h.NewStatus == ReservationStatus.Approved)),

            ReportKind.AdminAll => query,

            _ => query.Where(r => false)
        };


        if (f.From is { } from)
        {
            var fromUtc = ToUtcDay(from);
            query = query.Where(r => r.Slot.Date >= fromUtc);
        }

        if (f.To is { } to)
        {
            var toUtc = ToUtcDay(to);
            query = query.Where(r => r.Slot.Date <= toUtc);
        }

        if (f.Status is { } status)
            query = query.Where(r => r.CurrentStatus == status);

        if (f.SpaceId is { } spaceId)
            query = query.Where(r => r.SpaceId == spaceId);

        if (f.ResourceId is { } resourceId)
            query = query.Where(r => r.ReservationResources.Any(rr => rr.ResourceId == resourceId));

        if (f.UserId is { } userId)
            query = query.Where(r => r.UserId == userId);

        if (f.CareerId is { } careerId)
            query = query.Where(r => r.User.CareerId == careerId);

        return query;
    }

    private static DateTime ToUtcDay(DateOnly day)
        => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}
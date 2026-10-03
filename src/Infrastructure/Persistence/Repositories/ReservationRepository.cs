using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly AppDbContext _context;

    public ReservationRepository(AppDbContext context) => _context = context;

    public async Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Reservation?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.Reservations
            .Include(r => r.User).ThenInclude(u => u.Role)
            .Include(r => r.User).ThenInclude(u => u.Career)
            .Include(r => r.Space)
            .Include(r => r.Voucher)
            .Include(r => r.ReservationResources).ThenInclude(rr => rr.Resource)
            .Include(r => r.ReservationHistories).ThenInclude(h => h.ChangedBy).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(Reservation reservation) => _context.Reservations.Add(reservation);

    public void Update(Reservation reservation) => _context.Reservations.Update(reservation);

    public async Task<IEnumerable<Reservation>> GetActiveBySpaceAndDateAsync(Guid spaceId, DateTime date, CancellationToken ct = default)
        => await _context.Reservations
            .Where(r => r.SpaceId == spaceId
                && r.Slot.Date == date.Date
                && r.CurrentStatus != ReservationStatus.Rejected
                && r.CurrentStatus != ReservationStatus.Cancelled)
            .ToListAsync(ct);

    public async Task<IEnumerable<Reservation>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.Reservations
        .Include(r => r.User)
            .ThenInclude(u => u.Role)
        .Include(r => r.User)
            .ThenInclude(u => u.Career)
        .Include(r => r.Space)
        .Where(r => r.UserId == userId)
        .OrderByDescending(r => r.Slot.Date)
        .ToListAsync(ct);


    public async Task<IEnumerable<Reservation>> GetAllExcludingDraftAsync(CancellationToken ct = default)
        => await _context.Reservations
            .Include(r => r.User).ThenInclude(u => u.Role)
            .Include(r => r.User).ThenInclude(u => u.Career)
            .Include(r => r.Space)
            .Where(r => r.CurrentStatus != ReservationStatus.Draft)
            .OrderByDescending(r => r.Slot.Date)
            .ToListAsync(ct);

    public async Task<IEnumerable<Reservation>> GetForVicerrectorReviewAsync(CancellationToken ct = default)
        => await _context.Reservations
            .Include(r => r.User).ThenInclude(u => u.Role)
            .Include(r => r.User).ThenInclude(u => u.Career)
            .Include(r => r.Space)
            .Where(r => r.CurrentStatus == ReservationStatus.PendingVicerrector
                || r.CurrentStatus == ReservationStatus.PendingAssets
                || r.CurrentStatus == ReservationStatus.Approved
                || r.CurrentStatus == ReservationStatus.Rejected)
            .OrderByDescending(r => r.Slot.Date)
            .ToListAsync(ct);

    public async Task<IEnumerable<Reservation>> GetByCareerAsync(Guid careerId, CancellationToken ct = default)
        => await _context.Reservations
            .Include(r => r.User).ThenInclude(u => u.Role)
            .Include(r => r.User).ThenInclude(u => u.Career)
            .Include(r => r.Space)
            .Where(r => r.User.CareerId == careerId && r.CurrentStatus != ReservationStatus.Draft)
            .OrderByDescending(r => r.Slot.Date)
            .ToListAsync(ct);


    public void AddHistory(ReservationHistory history) => _context.ReservationHistories.Add(history);
    public void RemoveResource(ReservationResource resource) => _context.ReservationResources.Remove(resource);

    public void AddResource(ReservationResource resource) => _context.ReservationResources.Add(resource);
}
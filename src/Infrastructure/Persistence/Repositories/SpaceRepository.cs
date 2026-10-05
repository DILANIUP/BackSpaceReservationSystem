using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;
using SpaceReservationSystem.Domain.Enums;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class SpaceRepository : ISpaceRepository
{
    private readonly AppDbContext _context;

    public SpaceRepository(AppDbContext context) => _context = context;

    public async Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Spaces.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<Space>> GetAllAsync(CancellationToken ct = default)
        => await _context.Spaces.ToListAsync(ct);

    public async Task<IEnumerable<Space>> GetAvailableAsync(
    DateTime date, TimeSpan startTime, TimeSpan endTime,
    Guid? excludeReservationId, CancellationToken ct = default)
    => await _context.Spaces
        .Where(s => s.IsActive
            // Excluye el espacio si existe una reserva activa que se solape
            // en fecha Y hora (inicio < fin ajeno && inicio ajeno < fin).
            && !_context.Reservations.Any(r =>
                r.SpaceId == s.Id
                && r.Id != excludeReservationId
                && r.Slot.Date == date.Date
                && r.CurrentStatus != ReservationStatus.Rejected
                && r.CurrentStatus != ReservationStatus.Cancelled
                && r.Slot.StartTime < endTime
                && startTime < r.Slot.EndTime))
        .OrderBy(s => s.Name)
        .ToListAsync(ct);

    public void Add(Space space) => _context.Spaces.Add(space);

    public void Update(Space space) => _context.Spaces.Update(space);
}
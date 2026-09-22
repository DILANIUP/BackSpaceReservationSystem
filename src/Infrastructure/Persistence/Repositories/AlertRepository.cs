using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class AlertRepository : IAlertRepository
{
    private readonly AppDbContext _context;

    public AlertRepository(AppDbContext context) => _context = context;

    public async Task<Alert?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<Alert>> GetAllAsync(CancellationToken ct = default)
    => await _context.Alerts.ToListAsync(ct);

    public void Add(Alert alert) => _context.Alerts.Add(alert);

    public void Update(Alert alert) => _context.Alerts.Update(alert);
}
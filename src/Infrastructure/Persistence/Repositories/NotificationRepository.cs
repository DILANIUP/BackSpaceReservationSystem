using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
        => _context = context;

    public async Task<List<Notification>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
        => await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public void Add(Notification notification)
        => _context.Notifications.Add(notification);

    public void Update(Notification notification)
        => _context.Notifications.Update(notification);
}
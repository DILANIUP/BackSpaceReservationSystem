using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface INotificationRepository
{
    Task<List<Notification>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default);

    void Add(Notification notification);

    void Update(Notification notification);
}
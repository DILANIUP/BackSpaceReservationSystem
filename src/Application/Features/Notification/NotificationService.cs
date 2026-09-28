using NotificationEntity = SpaceReservationSystem.Domain.Entities.Notification;
using SpaceReservationSystem.Domain.Interfaces;

namespace SpaceReservationSystem.Application.Features.Notification;

public class NotificationService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(
        INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<List<NotificationEntity>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _notificationRepository.GetByUserIdAsync(userId, ct);
    }
}
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

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(
            notificationId,
            ct);

        if (notification is null || notification.UserId != userId)
            return false;

        notification.MarkAsRead();

        _notificationRepository.Update(notification);

        return true;
    }
}
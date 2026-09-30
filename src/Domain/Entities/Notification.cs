using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Guid? AlertId { get; private set; }
    public Alert Alert { get; private set; } = null!;

    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }

    private Notification(
        Guid id,
        Guid userId,
        Guid alertId,
        string message)
        : base(id)
    {
        UserId = userId;
        AlertId = alertId;
        Message = message.Trim();
        IsRead = false;
    }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        Guid alertId,
        string message)
    {
        return new Notification(
            Guid.NewGuid(),
            userId,
            alertId,
            message.Trim());
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
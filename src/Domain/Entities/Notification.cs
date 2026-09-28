using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }

    private Notification(
        Guid id,
        Guid userId,
        string message)
        : base(id)
    {
        UserId = userId;
        Message = message;
        IsRead = false;
    }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        string message)
    {
        return new Notification(
            Guid.NewGuid(),
            userId,
            message.Trim());
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
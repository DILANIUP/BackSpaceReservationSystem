namespace SpaceReservationSystem.Domain.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
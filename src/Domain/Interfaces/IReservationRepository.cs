using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Reservation?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Reservation>> GetActiveBySpaceAndDateAsync(Guid spaceId, DateTime date, CancellationToken ct = default);
    Task<IEnumerable<Reservation>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(Reservation reservation);
    void Update(Reservation reservation);
    void AddHistory(ReservationHistory history);
}
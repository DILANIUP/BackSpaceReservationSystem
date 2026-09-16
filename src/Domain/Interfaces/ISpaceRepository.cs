using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface ISpaceRepository
{
    Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Space>> GetAllActiveAsync(CancellationToken ct = default);
    void Add(Space space);
    void Update(Space space);
}
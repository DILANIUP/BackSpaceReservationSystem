using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface IResourceRepository
{
    Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Resource>> GetAllActiveAsync(CancellationToken ct = default);
    void Add(Resource resource);
    void Update(Resource resource);
}
using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface IAlertRepository
{
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Alert>> GetAllAsync(CancellationToken ct = default);
    void Add(Alert alert);
    void Update(Alert alert);
}
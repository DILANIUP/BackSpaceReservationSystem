using SpaceReservationSystem.Domain.Entities;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface ISpaceRepository
{
    Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Space>> GetAllAsync(CancellationToken ct = default);
    // Espacios activos sin ninguna reserva activa que se solape con el rango.
    // excludeReservationId: la reserva que se está asignando, para que su
    // propio espacio tentativo no cuente como "ocupado".
    Task<IEnumerable<Space>> GetAvailableAsync(
        DateTime date, TimeSpan startTime, TimeSpan endTime,
        Guid? excludeReservationId, CancellationToken ct = default);
    void Add(Space space);
    void Update(Space space);
}
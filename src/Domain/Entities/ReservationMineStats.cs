namespace SpaceReservationSystem.Domain.Models;

// NUEVO: datos agregados de una reserva para la lista /mine. Se calculan en la base
// (conteo y EXISTS), sin cargar los recursos ni el historial completos.
public sealed record ReservationMineStats(Guid ReservationId, int ResourceCount, bool HasHistory);
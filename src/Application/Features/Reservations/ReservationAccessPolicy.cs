using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;

namespace SpaceReservationSystem.Application.Features.Reservations;

// NUEVO: una sola regla de "quién puede ver una reserva", compartida por el detalle
// (GET /api/reservations/{id}) y la descarga del voucher. Así no se copia en dos
// servicios y no se desincroniza.
public class ReservationAccessPolicy(IUserRepository userRepository)
{
    public async Task<bool> CanViewAsync(Reservation reservation, Guid actingUserId, RoleCode actingRole, CancellationToken ct)
    {
        // El dueño siempre puede ver su propia reserva, sea cual sea su rol.
        if (reservation.UserId == actingUserId)
            return true;

        // Vicerrector, Bienes y Admin autorizan a nivel institucional: ven todas.
        if (actingRole is RoleCode.Admin or RoleCode.Vicerrector or RoleCode.Bienes)
            return true;

        // Coordinator: solo las de su carrera. La carrera se lee de la BD porque el
        // JWT no trae careerId.
        if (actingRole == RoleCode.Coordinator)
        {
            var coordinator = await userRepository.GetByIdAsync(actingUserId, ct);

            // Sin carrera asignada no ve nada ajeno (evita que null == null dé true).
            if (coordinator?.CareerId is null)
                return false;

            // Se busca al dueño por su Id en vez de usar reservation.User, porque
            // GetByIdAsync de reservas NO carga esa relación (vendría null).
            var owner = await userRepository.GetByIdAsync(reservation.UserId, ct);
            return owner?.CareerId == coordinator.CareerId;
        }

        // Student y Teacher: solo las propias (ya devueltas arriba).
        return false;
    }
}
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Application.Features.Vouchers;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Domain.ValueObjects;
using System.Text.RegularExpressions;

namespace SpaceReservationSystem.Application.Features.Reservations;

public class ReservationService(
    IReservationRepository reservationRepository,
    ISpaceRepository spaceRepository,
    IResourceRepository resourceRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateReservationRequest> validator,
    VoucherService voucherService
)
{
    public async Task<Result<ReservationResponse>> CreateAsync(CreateReservationRequest request, Guid actingUserId, RoleCode actingRole, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var f = validation.Errors[0];
            return Result.Failure<ReservationResponse>(Error.Validation(f.PropertyName, f.ErrorMessage));
        }

        // NUEVO: si viene OnBehalfOfUserId, la reserva es PARA otro usuario, no
        // para quien está logueado. targetUserId es el que queda como dueño real
        // (así le sigue apareciendo en "Mis reservas" a él/ella, no a quien la creó).
        var targetUserId = actingUserId;

        if (request.OnBehalfOfUserId is not null)
        {
            if (actingRole is RoleCode.Student or RoleCode.Teacher)
                return Result.Failure<ReservationResponse>(
                    Error.Validation("OnBehalfOfUserId", "No tienes permiso para crear reservas a nombre de otro usuario."));

            var targetUser = await userRepository.GetByIdAsync(request.OnBehalfOfUserId.Value, ct);
            if (targetUser is null)
                return Result.Failure<ReservationResponse>(Error.NotFound("User", request.OnBehalfOfUserId.Value.ToString()));

            if (actingRole == RoleCode.Coordinator)
            {
                var actingCoordinator = await userRepository.GetByIdAsync(actingUserId, ct);

                if (targetUser.Role?.Code is not (RoleCode.Student or RoleCode.Teacher))
                    return Result.Failure<ReservationResponse>(
                        Error.Validation("OnBehalfOfUserId", "Un Coordinador solo puede crear reservas para estudiantes o docentes."));

                if (actingCoordinator?.CareerId is null || targetUser.CareerId != actingCoordinator.CareerId)
                    return Result.Failure<ReservationResponse>(
                        Error.Validation("OnBehalfOfUserId", "Solo puedes crear reservas para usuarios de tu misma carrera."));
            }
            // Vicerrector, Bienes y Admin: sin restricción de a quién.

            targetUserId = targetUser.Id;
        }
        var normalizedDate = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        var hasResources = request.Resources is { Count: > 0 };

        if (request.SpaceId is null && !hasResources)
            return Result.Failure<ReservationResponse>(
                Error.Validation("Reservation", "Debe incluir al menos un Space o un Resource."));

        var reservationResult = Reservation.Create(
            normalizedDate, request.StartTime, request.EndTime, request.Reason, targetUserId, request.SpaceId
        );

        if (reservationResult.IsFailure)
            return Result.Failure<ReservationResponse>(reservationResult.Error);

        var reservation = reservationResult.Value;

        if (request.SpaceId is not null)
        {
            var space = await spaceRepository.GetByIdAsync(request.SpaceId.Value, ct);
            if (space is null)
                return Result.Failure<ReservationResponse>(Error.NotFound("Space", request.SpaceId.Value.ToString()));

            if (!space.IsActive)
                return Result.Failure<ReservationResponse>(Error.Conflict("Space", "El espacio está inactivo y no puede reservarse."));

            var existing = await reservationRepository.GetActiveBySpaceAndDateAsync(request.SpaceId.Value, normalizedDate, ct);

            if (existing.Any(r => r.Slot.Overlaps(reservation.Slot)))
                return Result.Failure<ReservationResponse>(ReservationErrors.SlotAlreadyTaken);
        }

        if (hasResources)
        {
            foreach (var item in request.Resources!)
            {
                var resource = await resourceRepository.GetByIdAsync(item.ResourceId, ct);

                if (resource is null)
                    return Result.Failure<ReservationResponse>(
                        Error.NotFound("Resource", item.ResourceId.ToString()));

                if (!resource.Status)
                    return Result.Failure<ReservationResponse>(
                        Error.Conflict("Resource", $"El recurso '{resource.Name}' está inactivo."));

                var existingReservations = await reservationRepository
                    .GetActiveByResourceAndDateAsync(resource.Id, normalizedDate, ct);

                var reservedQuantity = existingReservations
                    .Where(r => r.Slot.Overlaps(reservation.Slot))
                    .SelectMany(r => r.ReservationResources)
                    .Where(rr => rr.ResourceId == resource.Id)
                    .Sum(rr => rr.RequestedQuantity);

                var availableQuantity = resource.AvailableQuantity - reservedQuantity;

                if (item.Quantity > availableQuantity)
                {
                    return Result.Failure<ReservationResponse>(
                        Error.Conflict(
                            "Resource",
                            $"Cantidad insuficiente para '{resource.Name}'. Disponible: {Math.Max(0, availableQuantity)}."));
                }

                var reservationResourceResult = ReservationResource.Create(
                    item.Quantity,
                    reservation.Id,
                    resource.Id);

                if (reservationResourceResult.IsFailure)
                    return Result.Failure<ReservationResponse>(reservationResourceResult.Error);

                reservation.ReservationResources.Add(reservationResourceResult.Value);
            }
        }

        reservationRepository.Add(reservation);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReservationResponse(
            reservation.Id, reservation.Slot.Date, reservation.Slot.StartTime, reservation.Slot.EndTime,
            reservation.Reason, reservation.CurrentStatus.ToString(), reservation.UserId, reservation.SpaceId
        );
    }

    // editar un Draft propio 
    public async Task<Result<ReservationResponse>> EditAsync(Guid id, EditReservationRequest request, Guid actingUserId, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdWithDetailsAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", id.ToString()));

        if (reservation.UserId != actingUserId)
            return Result.Failure<ReservationResponse>(
                Error.Validation("Reservation", "No puedes editar una reserva que no es tuya."));

        //var normalizedDate = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        var normalizedDate = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Unspecified);
        var editResult = reservation.Edit(normalizedDate, request.StartTime, request.EndTime, request.Reason, request.SpaceId);
        if (editResult.IsFailure)
            return Result.Failure<ReservationResponse>(editResult.Error);

        // Se marca explícitamente cada recurso viejo como eliminado y cada uno
        // nuevo como agregado directo en el DbSet (en vez de mutar la colección
        // de navegación con .Clear()/.Add()) — así no queda ambigüedad para que
        // EF intente "emparejar" un recurso viejo con uno nuevo como si fuera un
        // simple cambio de valores. Esto corre SIEMPRE, tenga o no recursos nuevos,
        // porque el usuario puede estar quitando todos los recursos de la reserva.
        foreach (var old in reservation.ReservationResources.ToList())
        {
            reservation.ReservationResources.Remove(old);
            reservationRepository.RemoveResource(old);
        }

        if (request.Resources is { Count: > 0 })
        {
            foreach (var item in request.Resources)
            {
                var resource = await resourceRepository.GetByIdAsync(item.ResourceId, ct);

                if (resource is null)
                    return Result.Failure<ReservationResponse>(Error.NotFound("Resource", item.ResourceId.ToString()));

                if (!resource.Status)
                    return Result.Failure<ReservationResponse>(
                        Error.Conflict("Resource", $"El recurso '{resource.Name}' está inactivo."));

                if (item.Quantity > resource.AvailableQuantity)
                    return Result.Failure<ReservationResponse>(
                        Error.Conflict("Resource", $"Cantidad insuficiente para '{resource.Name}'."));

                var reservationResourceResult = ReservationResource.Create(item.Quantity, reservation.Id, resource.Id);
                if (reservationResourceResult.IsFailure)
                    return Result.Failure<ReservationResponse>(reservationResourceResult.Error);

                reservation.ReservationResources.Add(reservationResourceResult.Value);
                reservationRepository.AddResource(reservationResourceResult.Value);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ToResponse(reservation);
    }

    public async Task<IEnumerable<ResourceAvailabilityResponse>> GetResourceAvailabilityAsync(
    DateTime date,
    TimeSpan startTime,
    TimeSpan endTime,
    CancellationToken ct)
    {
        // Normaliza la fecha a UTC para que PostgreSQL pueda compararla con timestamp with time zone
        //var normalizedDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var normalizedDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var slotResult = ReservationSlot.Create(normalizedDate, startTime, endTime);

        if (slotResult.IsFailure)
            return [];

        var requestedSlot = slotResult.Value;

        var resources = await resourceRepository.GetAllAsync(ct);

        var result = new List<ResourceAvailabilityResponse>();

        foreach (var resource in resources.Where(r => r.Status))
        {
            var reservations = await reservationRepository
                .GetActiveByResourceAndDateAsync(resource.Id, normalizedDate, ct);

            var reserved = reservations
                .Where(r => r.Slot.Overlaps(requestedSlot))
                .SelectMany(r => r.ReservationResources)
                .Where(rr => rr.ResourceId == resource.Id)
                .Sum(rr => rr.RequestedQuantity);

            result.Add(new ResourceAvailabilityResponse(
                resource.Id,
                resource.Name,
                resource.AvailableQuantity,
                reserved,
                Math.Max(0, resource.AvailableQuantity - reserved)
            ));
        }

        return result;
    }
    public async Task<IEnumerable<ReservationResponse>> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var reservations = await reservationRepository.GetByUserIdAsync(userId, ct);
        return reservations.Select(ToResponse);
    }
    // Calcula la disponibilidad de cada recurso según la fecha y el horario solicitado
    public async Task<Result<IEnumerable<ReservationResponse>>> GetByCareerAsync(Guid actingUserId, RoleCode actingRole, CancellationToken ct)
    {
        IEnumerable<Reservation> reservations;

        if (actingRole == RoleCode.Admin)
        {
            // Admin no tiene carrera asignada: ve todas las solicitudes ya enviadas.
            reservations = await reservationRepository.GetAllExcludingDraftAsync(ct);
        }
        else
        {
            var coordinator = await userRepository.GetByIdAsync(actingUserId, ct);
            if (coordinator is null)
                return Result.Failure<IEnumerable<ReservationResponse>>(Error.NotFound("User", actingUserId.ToString()));

            if (coordinator.CareerId is null)
                return Result.Failure<IEnumerable<ReservationResponse>>(
                    Error.Conflict("User", "El coordinador no tiene una carrera asignada."));

            reservations = await reservationRepository.GetByCareerAsync(coordinator.CareerId.Value, ct);
        }

        return Result.Success(reservations.Select(ToResponse));
    }

    // NUEVO: bandeja del Vicerrector. A diferencia de GetByCareerAsync, acá no
    // hay ninguna rama por CareerId — el Vicerrector autoriza institucionalmente,
    // ve todas las carreras. El repositorio ya filtra a "lo que pasó por
    // Coordinador" (ver GetForVicerrectorReviewAsync).
    public async Task<Result<IEnumerable<ReservationResponse>>> GetForVicerrectorAsync(CancellationToken ct)
    {
        var reservations = await reservationRepository.GetForVicerrectorReviewAsync(ct);
        return Result.Success(reservations.Select(ToResponse));
    }

    // NUEVO: bandeja de Bienes. A diferencia de GetByCareerAsync, sin filtro por carrera.
    // REsuamos response para no repetir al mapeo
    public async Task<Result<IEnumerable<ReservationResponse>>> GetForAssetsAsync(CancellationToken ct)
    {
        var reservations = await reservationRepository.GetForAssetsReviewAsync(ct);
        return Result.Success(reservations.Select(ToResponse));
    }

    // NUEVO: se extrajo el mapeo Reservation -> ReservationResponse a un método
    // propio porque ya lo estábamos repitiendo igual en GetByCareerAsync y ahora
    // en GetForVicerrectorAsync. "static" porque no usa ningún campo de la clase,
    // solo el parámetro que recibe.
    private static ReservationResponse ToResponse(Reservation r) => new(
        r.Id, r.Slot.Date, r.Slot.StartTime, r.Slot.EndTime,
        r.Reason, r.CurrentStatus.ToString(), r.UserId, r.SpaceId,
        r.User?.Name, r.User?.Role?.Name, r.Space?.Name, r.User?.Career?.Name
    );

    // CAMBIO: antes devolvía un ReservationResponse "pelado" (ni siquiera traía
    // RequesterName/SpaceName, a diferencia de las listas). Ahora devuelve
    // ReservationDetailResponse, con Resources e History mapeados —
    // reservation.ReservationResources/ReservationHistories ya vienen cargados
    // porque GetByIdWithDetailsAsync los incluye (ver el repo).
    public async Task<Result<ReservationDetailResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdWithDetailsAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationDetailResponse>(Error.NotFound("Reservation", id.ToString()));

        return new ReservationDetailResponse(
            reservation.Id, reservation.Slot.Date, reservation.Slot.StartTime, reservation.Slot.EndTime,
            reservation.Reason, reservation.CurrentStatus.ToString(), reservation.UserId, reservation.SpaceId,
            reservation.User?.Name, reservation.User?.Role?.Name, reservation.Space?.Name, reservation.User?.Career?.Name,
            reservation.ReservationResources
                .Select(rr => new ReservationResourceItemResponse(rr.Resource?.Name ?? "—", rr.RequestedQuantity))
                .ToList(),
            reservation.ReservationHistories
                // más reciente primero no interesa acá: el modal quiere ver el
                // avance en orden cronológico (más antiguo arriba, "Solicitud
                // registrada" primero), como en el mockup.
                .OrderBy(h => h.ChangeDate)
                .Select(h => new ReservationHistoryEntryResponse(
                    h.NewStatus.ToString(), h.Justification, h.ChangeDate,
                    h.ChangedBy?.Name ?? "—", h.ChangedBy?.Role?.Name))
                .ToList()
        );
    }



    public async Task<Result<ReservationResponse>> SubmitAsync(Guid id, Guid userId, RoleCode actingRole, string justification, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", id.ToString()));

        var esDueño = reservation.UserId == userId;
        var esQuienLaCreo = reservation.CreatedBy == userId;

        if (!esDueño && !esQuienLaCreo)
            return Result.Failure<ReservationResponse>(
                Error.Conflict("Reservation", "No puedes enviar una reserva que no te pertenece ni creaste."));

        var result = await TransitionAsync(id, userId, justification, r => r.SubmitByRole(actingRole), requirementOwnerShip: false, ct);

        // Admin salta todas las etapas y su reserva queda Approved de inmediato.
        // Como el voucher normalmente se genera en "assign", acá hay que
        // generarlo a mano para que no quede aprobada sin comprobante.
        if (result.IsSuccess && result.Value.CurrentStatus == nameof(ReservationStatus.Approved))
        {
            var voucherResult = await voucherService.GenerateAsync(new GenerateVoucherRequest(id), ct);
            if (voucherResult.IsFailure)
                return Result.Failure<ReservationResponse>(voucherResult.Error);
        }

        return result;
    }

    public Task<Result<ReservationResponse>> ElevateToVicerrectorAsync(Guid id, Guid userId, string justification, CancellationToken ct)
        => TransitionAsync(id, userId, justification, r => r.ElevatedToVicerrector(), requirementOwnerShip: false, ct);

    public Task<Result<ReservationResponse>> ApproveByVicerrectorAsync(Guid id, Guid userId, string justification, CancellationToken ct)
        => TransitionAsync(id, userId, justification, r => r.ApproveByVicerrector(), requirementOwnerShip: false, ct);

    public async Task<Result<ReservationResponse>> AssignBySpaceManagementAsync(
        Guid id, Guid userId, string justification, Guid? newSpaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justification))
            return Result.Failure<ReservationResponse>(ReservationHistoryErrors.InvalidJustification);

        var reservation = await reservationRepository.GetByIdWithDetailsAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", id.ToString()));

        if (newSpaceId is not null)
        {
            var space = await spaceRepository.GetByIdAsync(newSpaceId.Value, ct);
            if (space is null)
                return Result.Failure<ReservationResponse>(Error.NotFound("Space", newSpaceId.Value.ToString()));

            if (!space.IsActive)
                return Result.Failure<ReservationResponse>(Error.Conflict("Space", "El espacio está inactivo y no puede asignarse."));

            var existing = await reservationRepository.GetActiveBySpaceAndDateAsync(newSpaceId.Value, reservation.Slot.Date, ct);

            if (existing.Any(r => r.Id != reservation.Id && r.Slot.Overlaps(reservation.Slot)))
                return Result.Failure<ReservationResponse>(ReservationErrors.SlotAlreadyTaken);
        }

        var previousStatus = reservation.CurrentStatus;
        var transitionResult = reservation.AssignBySpaceManagement(newSpaceId);
        if (transitionResult.IsFailure)
            return Result.Failure<ReservationResponse>(transitionResult.Error);

        var historyResult = ReservationHistory.Create(previousStatus, reservation.CurrentStatus, justification, userId, reservation.Id);
        if (historyResult.IsFailure)
            return Result.Failure<ReservationResponse>(historyResult.Error);

        reservation.ReservationHistories.Add(historyResult.Value);
        reservationRepository.AddHistory(historyResult.Value);
        await unitOfWork.SaveChangesAsync(ct);

        var voucherResult = await voucherService.GenerateAsync(new GenerateVoucherRequest(id), ct);
        if (voucherResult.IsFailure)
            return Result.Failure<ReservationResponse>(voucherResult.Error);

        return new ReservationResponse(
            reservation.Id, reservation.Slot.Date, reservation.Slot.StartTime, reservation.Slot.EndTime,
            reservation.Reason, reservation.CurrentStatus.ToString(), reservation.UserId, reservation.SpaceId
        );
    }

    private static readonly Dictionary<RoleCode, ReservationStatus> RejectionAuthority = new()
    {
        [RoleCode.Coordinator] = ReservationStatus.PendingCoordinator,
        [RoleCode.Vicerrector] = ReservationStatus.PendingVicerrector,
        [RoleCode.Bienes] = ReservationStatus.PendingAssets
    };

    public async Task<Result<ReservationResponse>> RejectAsync(
        Guid id, Guid userId, RoleCode actingRole, string justification, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdWithDetailsAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", id.ToString()));

        if (actingRole != RoleCode.Admin
            && RejectionAuthority.TryGetValue(actingRole, out var expectedStatus)
            && reservation.CurrentStatus != expectedStatus)
        {
            return Result.Failure<ReservationResponse>(
                Error.Conflict("Reservation", "No puedes rechazar una reserva que no está en tu etapa actual."));
        }

        return await TransitionAsync(id, userId, justification, r => r.Reject(actingRole), requirementOwnerShip: false, ct);
    }

    public Task<Result<ReservationResponse>> CancelAsync(Guid id, Guid userId, string justification, CancellationToken ct)
        => TransitionAsync(id, userId, justification, r => r.Cancel(), requirementOwnerShip: true, ct);

    private async Task<Result<ReservationResponse>> TransitionAsync(
        Guid reservationId,
        Guid actingUserId,
        string justification,
        Func<Reservation, Result> transition,
        bool requirementOwnerShip,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(justification))
            return Result.Failure<ReservationResponse>(ReservationHistoryErrors.InvalidJustification);

        var reservation = await reservationRepository.GetByIdWithDetailsAsync(reservationId, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", reservationId.ToString()));

        if (requirementOwnerShip && reservation.UserId != actingUserId)
            return Result.Failure<ReservationResponse>(
                Error.Conflict("Reservation", "No puedes modificar una reserva que no te pertenece."));

        var previousStatus = reservation.CurrentStatus;

        var transitionResult = transition(reservation);
        if (transitionResult.IsFailure)
            return Result.Failure<ReservationResponse>(transitionResult.Error);

        var historyResult = ReservationHistory.Create(
            previousStatus, reservation.CurrentStatus, justification, actingUserId, reservation.Id
        );

        if (historyResult.IsFailure)
            return Result.Failure<ReservationResponse>(historyResult.Error);

        reservation.ReservationHistories.Add(historyResult.Value);
        reservationRepository.AddHistory(historyResult.Value);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReservationResponse(
            reservation.Id, reservation.Slot.Date, reservation.Slot.StartTime, reservation.Slot.EndTime, reservation.Reason,
            reservation.CurrentStatus.ToString(), reservation.UserId, reservation.SpaceId
        );
    }
}
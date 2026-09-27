using FluentValidation;
using SpaceReservationSystem.Application.Features.Vouchers;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

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
                    return Result.Failure<ReservationResponse>(Error.NotFound("Resource", item.ResourceId.ToString()));

                if (!resource.Status)
                    return Result.Failure<ReservationResponse>(Error.Conflict("Resource", $"El recurso '{resource.Name}' está inactivo."));

                if (item.Quantity > resource.AvailableQuantity)
                    return Result.Failure<ReservationResponse>(Error.Conflict("Resource", $"Cantidad insuficiente para '{resource.Name}'."));

                var reservationResourceResult = ReservationResource.Create(item.Quantity, reservation.Id, resource.Id);
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

        var editResult = reservation.Edit(request.Date, request.StartTime, request.EndTime, request.Reason, request.SpaceId);
        if (editResult.IsFailure)
            return Result.Failure<ReservationResponse>(editResult.Error);

        // Se borran los recursos anteriores y se validan/agregan los nuevos —
        // más simple que comparar "qué cambió" recurso por recurso, y el
        // volumen de datos es chico (unas pocas filas por reserva).
        reservation.ReservationResources.Clear();

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
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ToResponse(reservation);
    }
    public async Task<IEnumerable<ReservationResponse>> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var reservations = await reservationRepository.GetByUserIdAsync(userId, ct);
        return reservations.Select(ToResponse);
    }

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

    public Task<Result<ReservationResponse>> SubmitToCoordinatorAsync(Guid id, Guid userId, string justification, CancellationToken ct)
        => TransitionAsync(id, userId, justification, r => r.SubmitToCoordinator(), requirementOwnerShip: true, ct);

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
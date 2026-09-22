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
    public async Task<Result<ReservationResponse>> CreateAsync(CreateReservationRequest request, Guid userId, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var f = validation.Errors[0];
            return Result.Failure<ReservationResponse>(Error.Validation(f.PropertyName, f.ErrorMessage));
        }

        var normalizedDate = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        var hasResources = request.Resources is { Count: > 0 };

        if (request.SpaceId is null && !hasResources) 
            return Result.Failure<ReservationResponse>(
                Error.Validation("Reservation", "Debe incluir al menos un Space o un Resource."));

        var reservationResult = Reservation.Create(
            normalizedDate, request.StartTime, request.EndTime, request.Reason, userId, request.SpaceId
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

    public async Task<IEnumerable<ReservationResponse>> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var reservations = await reservationRepository.GetByUserIdAsync(userId, ct);
        return reservations.Select(r => new ReservationResponse(
            r.Id, r.Slot.Date, r.Slot.StartTime, r.Slot.EndTime,
            r.Reason, r.CurrentStatus.ToString(), r.UserId, r.SpaceId
        ));
    }

    public async Task<Result<IEnumerable<ReservationResponse>>> GetByCareerAsync(Guid coordinatorUserId, RoleCode roleCode, CancellationToken ct)
    {
        var coordinator = await userRepository.GetByIdAsync(coordinatorUserId, ct);
        if(coordinator is null)
            return Result.Failure<IEnumerable<ReservationResponse>>(Error.NotFound("User", coordinatorUserId.ToString()));

        if (coordinator.CareerId is null)
            return Result.Failure<IEnumerable<ReservationResponse>>(
                Error.Conflict("User", "El coordinador no tiene una carrera asignada."));

        var reservations = await reservationRepository.GetByCarrerAsync(coordinator.CareerId.Value, ct);

        return Result.Success(reservations.Select(r => new ReservationResponse(
            r.Id, r.Slot.Date, r.Slot.StartTime, r.Slot.EndTime,
            r.Reason, r.CurrentStatus.ToString(), r.UserId, r.SpaceId,
            r.User?.Name, r.User?.Role?.Name, r.Space?.Name
        )));
    }
    
    public async Task<Result<ReservationResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdWithDetailsAsync(id, ct);
        if (reservation is null)
            return Result.Failure<ReservationResponse>(Error.NotFound("Reservation", id.ToString()));

        return new ReservationResponse(
            reservation.Id, reservation.Slot.Date, reservation.Slot.StartTime, reservation.Slot.EndTime,
            reservation.Reason, reservation.CurrentStatus.ToString(), reservation.UserId, reservation.SpaceId
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

        return await TransitionAsync(id, userId, justification, r => r.Reject(), requirementOwnerShip: false, ct);
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
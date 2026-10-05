using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Domain.ValueObjects;

namespace SpaceReservationSystem.Domain.Entities;

public class Reservation : AuditableEntity
{
    private const int MinNoticeHours = 72;

    public ReservationSlot Slot { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public ReservationStatus CurrentStatus { get; private set; }
    public DateTime RequestDate { get; private set; }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Guid? SpaceId { get; private set; }
    public Space? Space { get; private set; }

    // Navegación inversa
    public Voucher? Voucher { get; private set; }

    public ICollection<ReservationResource> ReservationResources { get; private set; } = new List<ReservationResource>();
    public ICollection<ReservationHistory> ReservationHistories { get; private set; } = new List<ReservationHistory>();

    private Reservation(Guid id, ReservationSlot slot, string reason, Guid userId, Guid? spaceId)
        : base(id)
    {
        Slot = slot;
        Reason = reason;
        CurrentStatus = ReservationStatus.Draft;
        RequestDate = DateTime.UtcNow;
        UserId = userId;
        SpaceId = spaceId;
    }

    private Reservation() { }

    public static Result<Reservation> Create(
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        string reason,
        Guid userId,
        Guid? spaceId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<Reservation>(ReservationErrors.InvalidReason);

        if (userId == Guid.Empty)
            return Result.Failure<Reservation>(ReservationErrors.InvalidUser);

        var slotResult = ReservationSlot.Create(date, startTime, endTime);
        if (slotResult.IsFailure)
            return Result.Failure<Reservation>(slotResult.Error);

        if (date < DateTime.UtcNow.AddHours(MinNoticeHours))
            return Result.Failure<Reservation>(ReservationErrors.InsufficientNotice);

        return new Reservation(Guid.NewGuid(), slotResult.Value, reason.Trim(), userId, spaceId);
    }

    public Result SubmitToCoordinator()
    {
        if (CurrentStatus != ReservationStatus.Draft)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        CurrentStatus = ReservationStatus.PendingCoordinator;
        return Result.Success();
    }
    public Result SubmitByRole(RoleCode actingRole)
    {
        if (CurrentStatus != ReservationStatus.Draft)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        // Cada rol se salta las etapas que él mismo tendría que aprobar —
        // no tiene sentido que alguien revise/apruebe su propia solicitud.
        CurrentStatus = actingRole switch
        {
            RoleCode.Student or RoleCode.Teacher => ReservationStatus.PendingCoordinator,
            RoleCode.Coordinator => ReservationStatus.PendingVicerrector,
            RoleCode.Vicerrector => ReservationStatus.PendingAssets,
            RoleCode.Bienes => ReservationStatus.PendingVicerrector,
            RoleCode.Admin => ReservationStatus.Approved,
            _ => ReservationStatus.PendingCoordinator,
        };

        return Result.Success();
    }

    public Result ElevatedToVicerrector()
    {
        if (CurrentStatus != ReservationStatus.PendingCoordinator)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        CurrentStatus = ReservationStatus.PendingVicerrector;
        return Result.Success();
    }

    public Result ApproveByVicerrector()
    {
        if (CurrentStatus != ReservationStatus.PendingVicerrector)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        CurrentStatus = ReservationStatus.PendingAssets;
        return Result.Success();
    }

    public Result AssignBySpaceManagement(Guid? spaceId = null)
    {
        if (CurrentStatus != ReservationStatus.PendingAssets)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        // Si Bienes eligió un espacio, ese pasa a ser el definitivo.
        // Si viene null se conserva el que ya tenía la reserva (o ninguno,
        // en el caso de reservas solo de recursos).
        if (spaceId is not null)
            SpaceId = spaceId;

        CurrentStatus = ReservationStatus.Approved;
        return Result.Success();
    }

    public Result Reject(RoleCode actingRole)
    {
        if (CurrentStatus is ReservationStatus.Approved or ReservationStatus.Rejected)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        CurrentStatus = actingRole == RoleCode.Coordinator
            ? ReservationStatus.Draft
            : ReservationStatus.Rejected;

        return Result.Success();
    }

    public Result Cancel()
    {
        if (CurrentStatus is ReservationStatus.Rejected or ReservationStatus.Cancelled)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        CurrentStatus = ReservationStatus.Cancelled;
        return Result.Success();
    }

    public Result Update(DateTime date, TimeSpan startTime, TimeSpan endTime, string reason)
    {
        if (CurrentStatus != ReservationStatus.Draft)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReservationErrors.InvalidReason);

        var slotResult = ReservationSlot.Create(date, startTime, endTime);
        if (slotResult.IsFailure)
            return Result.Failure(slotResult.Error);

        if (date < DateTime.UtcNow.AddHours(MinNoticeHours))
            return Result.Failure(ReservationErrors.InsufficientNotice);

        Slot = slotResult.Value;
        Reason = reason.Trim();
        return Result.Success();
    }

    public Result Edit(DateTime date, TimeSpan startTime, TimeSpan endTime, string reason, Guid? spaceId)
    {
        if (CurrentStatus != ReservationStatus.Draft)
            return Result.Failure(ReservationErrors.InvalidStatusTransition);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReservationErrors.InvalidReason);

        var slotResult = ReservationSlot.Create(date, startTime, endTime);
        if (slotResult.IsFailure)
            return Result.Failure(slotResult.Error);

        if (date < DateTime.UtcNow.AddHours(MinNoticeHours))
            return Result.Failure(ReservationErrors.InsufficientNotice);

        Slot.UpdateFrom(slotResult.Value);
        Reason = reason.Trim();
        SpaceId = spaceId;
        return Result.Success();
    }
}
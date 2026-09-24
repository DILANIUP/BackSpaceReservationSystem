using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.Entities;

public class Alert : AuditableEntity
{
    public AlertType Type { get; private set; }
    public string Description { get; private set; } = null!;
    //public DateTime CreatedAt { get; private set; } Se elimina, ya que esta es heredada automaticamente, estaba duplicada
    public DateTime? ResolvedAt { get; private set; }
    public bool IsResolved { get; private set; }
    public string? ResolutionObservation { get; private set; }

    public Guid? ResourceId { get; private set; }
    public Resource? Resource { get; private set; }

    public Guid? SpaceId { get; private set; }
    public Space? Space { get; private set; }



    private Alert(Guid id, AlertType type, string description, Guid? resourceId, Guid? spaceId)
        : base(id)
    {
        Type = type;
        Description = description;
        ResourceId = resourceId;
        SpaceId = spaceId;
        IsResolved = false;
    }

    private Alert() { }

    public static Result<Alert> Create(
        AlertType type,
        string description,
        Guid? resourceId = null,
        Guid? spaceId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Result.Failure<Alert>(AlertErrors.InvalidDescription);

        if (resourceId is null && spaceId is null)
            return Result.Failure<Alert>(AlertErrors.MissingTarget);

        return new Alert(Guid.NewGuid(), type, description.Trim(), resourceId, spaceId);
    }

    public Result Resolve(string observation)
    {
        if (IsResolved)
            return Result.Failure(AlertErrors.AlreadyResolved);

        if (string.IsNullOrWhiteSpace(observation))
            return Result.Failure(AlertErrors.InvalidObservation);

        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
        ResolutionObservation = observation.Trim();

        return Result.Success();
    }
}


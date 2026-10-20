namespace SpaceReservationSystem.Domain.Primitives;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, string id) =>
    new($"{entity}.NotFound", $"{entity} with id '{id}' not found.");
    public static Error Conflict(string entity, string detail) => new($"{entity}", detail);
    public static Error Validation(string field, string detail) => new ($"Validation.{field}", detail);
    public static Error Forbidden(string detail) => new("Forbidden", detail); 
}
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.ValueObjects;

public sealed class ReservationSlot : IEquatable<ReservationSlot>
{

    private static readonly TimeSpan MinAllowedTime = TimeSpan.FromHours(7);
    private static readonly TimeSpan MaxAllowedTime = TimeSpan.FromHours(23);
    public DateTime Date { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }

    private ReservationSlot(DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
    }

    internal void UpdateFrom (ReservationSlot other)
    {
        Date = other.Date;
        StartTime = other.StartTime;
        EndTime = other.EndTime;
    }

    public static Result<ReservationSlot> Create(DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        if (date.Date < DateTime.UtcNow.Date)
            return Result.Failure<ReservationSlot>(ReservationErrors.InvalidDate);

        if (endTime <= startTime)
            return Result.Failure<ReservationSlot>(ReservationErrors.InvalidTimeRange);

        if (startTime < MinAllowedTime || endTime > MaxAllowedTime)
            return Result.Failure<ReservationSlot>(ReservationErrors.OutsideAllowedHours);

        return new ReservationSlot(date.Date, startTime, endTime);
    }

    public bool Overlaps(ReservationSlot other)
    {
        if (Date != other.Date)
            return false;

        return StartTime < other.EndTime && other.StartTime < EndTime;
    }

    public bool Equals(ReservationSlot? other) =>
        other is not null && Date == other.Date && StartTime == other.StartTime && EndTime == other.EndTime;

    public override bool Equals(object? obj) => Equals(obj as ReservationSlot);
    public override int GetHashCode() => HashCode.Combine(Date, StartTime, EndTime);
    public override string ToString() => $"{Date:yyyy-MM-dd} {StartTime}-{EndTime}";

}
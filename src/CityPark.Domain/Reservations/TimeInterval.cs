using CityPark.Domain.Common;

namespace CityPark.Domain.Reservations;

/// <summary>
/// Immutable UTC time interval [Start, End): start is included, end is not.
/// </summary>
public sealed record TimeInterval
{
    public TimeInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (start.Offset != TimeSpan.Zero)
        {
            throw new DomainException($"{nameof(Start)} must be in UTC, but has offset {start.Offset}.");
        }

        if (end.Offset != TimeSpan.Zero)
        {
            throw new DomainException($"{nameof(End)} must be in UTC, but has offset {end.Offset}.");
        }

        if (end <= start)
        {
            throw new DomainException($"{nameof(End)} ({end:O}) must be after {nameof(Start)} ({start:O}).");
        }

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeInterval other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Start < other.End && other.Start < End;
    }

    public bool Contains(DateTimeOffset instant) => Start <= instant && instant < End;
}

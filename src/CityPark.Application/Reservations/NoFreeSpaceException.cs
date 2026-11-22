using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Application.Reservations;

/// <summary>No space of the lot could be reserved for the interval: all were taken or lost to parallel requests.</summary>
public sealed class NoFreeSpaceException : DomainConflictException
{
    public NoFreeSpaceException(Guid lotId, TimeInterval interval)
        : base(
            $"Lot {lotId} has no free space for " +
            $"{(interval ?? throw new ArgumentNullException(nameof(interval))).Start:O} – {interval.End:O}.")
    {
        LotId = lotId;
        Interval = interval;
    }

    public Guid LotId { get; }

    public TimeInterval Interval { get; }
}

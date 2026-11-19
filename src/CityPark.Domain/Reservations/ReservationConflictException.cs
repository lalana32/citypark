using CityPark.Domain.Common;

namespace CityPark.Domain.Reservations;

/// <summary>The space already has an overlapping Active reservation (R6).</summary>
public class ReservationConflictException : DomainConflictException
{
    public ReservationConflictException(Guid spaceId, TimeInterval interval, Exception? innerException = null)
        : base(
            $"Space {spaceId} already has an active reservation that overlaps " +
            $"{interval.Start:O} – {interval.End:O}.",
            innerException)
    {
        SpaceId = spaceId;
        Interval = interval;
    }

    public Guid SpaceId { get; }

    public TimeInterval Interval { get; }
}

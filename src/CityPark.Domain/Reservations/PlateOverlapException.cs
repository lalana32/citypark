using CityPark.Domain.Common;

namespace CityPark.Domain.Reservations;

/// <summary>The plate already has an overlapping Active reservation, in any lot (R7).</summary>
public sealed class PlateOverlapException : DomainConflictException
{
    public PlateOverlapException(LicensePlate plate, TimeInterval interval, Exception? innerException = null)
        : base(
            $"Vehicle {plate} already has an active reservation that overlaps " +
            $"{(interval ?? throw new ArgumentNullException(nameof(interval))).Start:O} – {interval.End:O}.",
            innerException)
    {
        Plate = plate;
        Interval = interval;
    }

    public LicensePlate Plate { get; }

    public TimeInterval Interval { get; }
}

using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Domain.Lots;

/// <summary>
/// Daily window of a lot in local wall-clock time, the same every day (for example 07:00–22:00).
/// </summary>
public sealed record OpeningHours
{
    public OpeningHours(TimeOnly opensAt, TimeOnly closesAt)
    {
        if (closesAt <= opensAt)
        {
            throw new DomainException($"{nameof(ClosesAt)} ({closesAt:HH:mm}) must be after {nameof(OpensAt)} ({opensAt:HH:mm}).")
            {
                Field = nameof(ClosesAt),
            };
        }

        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public TimeOnly OpensAt { get; }

    public TimeOnly ClosesAt { get; }

    /// <summary>
    /// True when the interval lies within one local day and inside this window.
    /// The interval may end exactly at the closing time.
    /// </summary>
    public bool Contains(TimeInterval interval, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(timeZone);

        // Converting each UTC instant gives the offset valid at that instant,
        // so DST change days (23 or 25 hours long) are handled by the time zone rules.
        var localStart = TimeZoneInfo.ConvertTime(interval.Start, timeZone);
        var localEnd = TimeZoneInfo.ConvertTime(interval.End, timeZone);

        if (localStart.Date != localEnd.Date)
        {
            return false;
        }

        return OpensAt <= TimeOnly.FromTimeSpan(localStart.TimeOfDay)
            && TimeOnly.FromTimeSpan(localEnd.TimeOfDay) <= ClosesAt;
    }
}

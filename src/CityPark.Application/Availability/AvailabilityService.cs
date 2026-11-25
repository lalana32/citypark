using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Availability;

public sealed class AvailabilityService(IAppDbContext db, TimeProvider clock, TimeZoneInfo timeZone)
{
    public async Task<AvailabilityResult> GetAvailabilityAsync(
        Guid lotId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        // The client may send any offset (Z or +01:00); the domain works in UTC (ADR-004).
        var interval = new TimeInterval(start.ToUniversalTime(), end.ToUniversalTime());

        // The lot without its spaces: only the opening hours are needed to validate the interval.
        var lot = await db.ParkingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
            ?? throw new NotFoundException($"Parking lot {lotId} was not found.");

        Reservation.EnsureBookable(lot, interval, timeZone, clock);

        // Not executed here: it becomes a correlated NOT EXISTS subquery in the statement below.
        var blocking = db.Reservations.Where(Reservation.ActiveAndOverlapping(interval));

        // One SQL statement: every active space of the lot with a free flag, so total and free
        // come from the same snapshot.
        var spaces = await db.ParkingLots
            .Where(l => l.Id == lotId)
            .SelectMany(l => l.Spaces)
            .Where(s => s.IsActive)
            .OrderBy(s => s.Label)
            .Select(s => new SpaceAvailability(s.Id, s.Label, !blocking.Any(r => r.SpaceId == s.Id)))
            .ToListAsync(cancellationToken);

        return new AvailabilityResult(spaces);
    }
}

using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Lots;

public sealed class QuoteService(IAppDbContext db, TimeProvider clock, TimeZoneInfo timeZone)
{
    public async Task<Quote> GetQuoteAsync(
        Guid lotId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        // The client may send any offset (Z or +01:00); the domain works in UTC (ADR-004).
        var interval = new TimeInterval(start.ToUniversalTime(), end.ToUniversalTime());

        var lot = await db.ParkingLots
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
            ?? throw new NotFoundException($"Parking lot {lotId} was not found.");

        // The same rules as booking, so a quote is only given for an interval that can be reserved.
        Reservation.EnsureBookable(lot, interval, timeZone, clock);

        return new Quote(Rate.BilledHours(interval.Duration), lot.Rate.PriceFor(interval.Duration), lot.Rate.Currency);
    }
}

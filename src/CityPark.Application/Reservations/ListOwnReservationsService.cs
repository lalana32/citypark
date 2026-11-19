using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Reservations;

public sealed class ListOwnReservationsService(IAppDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<OwnReservation>> ListAsync(
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        // One clock reading, so every row on the page is judged at the same instant.
        var now = clock.GetUtcNow();

        // Read only: nothing is tracked, and only the columns the list shows are selected.
        // Lot → spaces → reservations, because a space has no navigation back to its lot.
        var own =
            from lot in db.ParkingLots.AsNoTracking()
            from space in lot.Spaces
            join reservation in db.Reservations on space.Id equals reservation.SpaceId
            where reservation.UserId == userId
            select new
            {
                reservation.Id,
                LotName = lot.Name,
                SpaceLabel = space.Label,
                reservation.StartTime,
                reservation.EndTime,
                reservation.Plate,
                reservation.Price,
                reservation.Currency,
                reservation.Status,
            };

        var totalCount = await own.CountAsync(cancellationToken);

        // Id breaks ties between equal start times, so OFFSET never repeats or skips a row.
        var rows = await own
            .OrderByDescending(r => r.StartTime)
            .ThenByDescending(r => r.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new OwnReservation(
                r.Id,
                r.LotName,
                r.SpaceLabel,
                new DateTimeOffset(r.StartTime, TimeSpan.Zero),
                new DateTimeOffset(r.EndTime, TimeSpan.Zero),
                r.Plate.Value,
                r.Price,
                r.Currency,
                Reservation.StateAt(r.Status, r.StartTime, r.EndTime, now)))
            .ToList();

        return new PagedResult<OwnReservation>(items, page.Page, page.PageSize, totalCount);
    }
}

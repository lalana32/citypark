using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Reservations;

/// <summary>Read side of the signed-in customer's own reservations: the list and one item.</summary>
public sealed class OwnReservationQueryService(IAppDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<OwnReservation>> ListAsync(
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        // One clock reading, so every row on the page is judged at the same instant.
        var now = clock.GetUtcNow();

        var own = Rows(userId);

        var totalCount = await own.CountAsync(cancellationToken);

        // Id breaks ties between equal start times, so OFFSET never repeats or skips a row.
        var rows = await own
            .OrderByDescending(r => r.StartTime)
            .ThenByDescending(r => r.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OwnReservation>([.. rows.Select(r => r.ToItem(now))], page.Page, page.PageSize, totalCount);
    }

    public async Task<OwnReservation> GetAsync(Guid reservationId, Guid userId, CancellationToken cancellationToken = default)
    {
        // The owner is part of the WHERE: a foreign reservation is "not found", exactly like a missing one (R9).
        var row = await Rows(userId).FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

        return row.ToItem(clock.GetUtcNow());
    }

    // Read only: nothing is tracked, and only the columns the list shows are selected.
    // Lot → spaces → reservations, because a space has no navigation back to its lot.
    private IQueryable<Row> Rows(Guid userId) =>
        from lot in db.ParkingLots.AsNoTracking()
        from space in lot.Spaces
        join reservation in db.Reservations on space.Id equals reservation.SpaceId
        where reservation.UserId == userId
        select new Row
        {
            Id = reservation.Id,
            LotName = lot.Name,
            SpaceLabel = space.Label,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
            Plate = reservation.Plate,
            Price = reservation.Price,
            Currency = reservation.Currency,
            Status = reservation.Status,
        };

    // A named type instead of an anonymous one, so both queries can share the projection.
    private sealed class Row
    {
        public Guid Id { get; init; }

        public string LotName { get; init; } = "";

        public string SpaceLabel { get; init; } = "";

        public DateTime StartTime { get; init; }

        public DateTime EndTime { get; init; }

        public LicensePlate Plate { get; init; } = null!;

        public decimal Price { get; init; }

        public string Currency { get; init; } = "";

        public ReservationStatus Status { get; init; }

        public OwnReservation ToItem(DateTimeOffset now) => new(
            Id,
            LotName,
            SpaceLabel,
            new DateTimeOffset(StartTime, TimeSpan.Zero),
            new DateTimeOffset(EndTime, TimeSpan.Zero),
            Plate.Value,
            Price,
            Currency,
            Reservation.StateAt(Status, StartTime, EndTime, now));
    }
}

using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Admin;

/// <summary>Read side of every user's reservations for the Admin role: the filtered list and one item.</summary>
public sealed class AdminReservationQueryService(IAppDbContext db, TimeZoneInfo timeZone, TimeProvider clock)
{
    public async Task<PagedResult<AdminReservation>> ListAsync(
        AdminReservationFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);

        var now = clock.GetUtcNow();

        var rows = Filtered(filter);

        var totalCount = await rows.CountAsync(cancellationToken);

        // Same total order as the customer's list: newest start first, id as the tie-breaker.
        var items = await rows
            .OrderByDescending(r => r.StartTime)
            .ThenByDescending(r => r.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReservation>([.. items.Select(r => r.ToItem(now))], page.Page, page.PageSize, totalCount);
    }

    public async Task<AdminReservation> GetAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var row = await Rows().FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException($"Reservation {reservationId} was not found.");

        return row.ToItem(clock.GetUtcNow());
    }

    private IQueryable<Row> Filtered(AdminReservationFilter filter)
    {
        var rows = Rows();

        if (filter.LotId is { } lotId)
        {
            rows = rows.Where(r => r.LotId == lotId);
        }

        if (filter.Status is { } status)
        {
            rows = rows.Where(r => r.Status == status);
        }

        // The extreme dates need no bound, and converting them would leave DateTime's range.
        if (filter.From is { } from && from > DateOnly.MinValue)
        {
            var fromUtc = StartOfDayUtc(from);
            rows = rows.Where(r => r.StartTime >= fromUtc);
        }

        if (filter.To is { } to && to < DateOnly.MaxValue)
        {
            var untilUtc = StartOfDayUtc(to.AddDays(1));
            rows = rows.Where(r => r.StartTime < untilUtc);
        }

        return rows;
    }

    // Local midnight, so a day is 23 or 25 hours long on the DST change days (ADR-004).
    private DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), timeZone);

    private IQueryable<Row> Rows() =>
        from lot in db.ParkingLots.AsNoTracking()
        from space in lot.Spaces
        join reservation in db.Reservations on space.Id equals reservation.SpaceId
        select new Row
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            LotId = lot.Id,
            LotName = lot.Name,
            SpaceId = space.Id,
            SpaceLabel = space.Label,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
            Plate = reservation.Plate,
            Price = reservation.Price,
            Currency = reservation.Currency,
            Status = reservation.Status,
        };

    private sealed class Row
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public Guid LotId { get; init; }

        public string LotName { get; init; } = "";

        public Guid SpaceId { get; init; }

        public string SpaceLabel { get; init; } = "";

        public DateTime StartTime { get; init; }

        public DateTime EndTime { get; init; }

        public LicensePlate Plate { get; init; } = null!;

        public decimal Price { get; init; }

        public string Currency { get; init; } = "";

        public ReservationStatus Status { get; init; }

        public AdminReservation ToItem(DateTimeOffset now) => new(
            Id,
            UserId,
            LotId,
            LotName,
            SpaceId,
            SpaceLabel,
            new DateTimeOffset(StartTime, TimeSpan.Zero),
            new DateTimeOffset(EndTime, TimeSpan.Zero),
            Plate.Value,
            Price,
            Currency,
            Status,
            Reservation.StateAt(Status, StartTime, EndTime, now));
    }
}

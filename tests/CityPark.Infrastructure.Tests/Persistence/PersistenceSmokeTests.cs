using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace CityPark.Infrastructure.Tests.Persistence;

// Saves through one DbContext and reloads through a fresh one, so every value really
// comes back from PostgreSQL and not from the first context's change tracker.
public sealed class PersistenceSmokeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // 1 December 2026 is winter time (UTC+1): 10:00 UTC is 11:00 local, inside the lot's hours.
    private static readonly DateTimeOffset Day = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static ParkingLot NewLot() => ParkingLot.Create(
        "Centar", "Ferhadija 1", new Rate(2.50m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));

    [Fact]
    public async Task Lot_WithSpaces_SavesAndReloads()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = NewLot();
        lot.AddSpace("A-1");
        lot.AddSpace("A-2").Deactivate();

        await using (var db = fixture.CreateDbContext())
        {
            db.ParkingLots.Add(lot);
            await db.SaveChangesAsync(ct);
        }

        await using var reloadDb = fixture.CreateDbContext();
        var reloaded = await reloadDb.ParkingLots
            .Include(l => l.Spaces)
            .SingleAsync(l => l.Id == lot.Id, ct);

        Assert.Equal("Centar", reloaded.Name);
        Assert.Equal("Ferhadija 1", reloaded.Address);
        Assert.Equal(new Rate(2.50m, "BAM"), reloaded.Rate);
        Assert.Equal(new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)), reloaded.OpeningHours);
        Assert.Equal(
            [("A-1", true), ("A-2", false)],
            reloaded.Spaces.OrderBy(s => s.Label).Select(s => (s.Label, s.IsActive)));
    }

    [Fact]
    public async Task Reservation_SavesAndReloads_WithUtcTimesAndExactPrice()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = NewLot();
        var space = lot.AddSpace("A-1");

        // 10:00–11:15 UTC: 75 minutes, billed as 2 hours at 2.50 BAM.
        var reservation = Reservation.Create(
            lot,
            space,
            new TimeInterval(Day.AddHours(10), Day.AddHours(11).AddMinutes(15)),
            new LicensePlate("A12K345"),
            fixture.UserId,
            Sarajevo,
            new FakeTimeProvider(Day));

        await using (var db = fixture.CreateDbContext())
        {
            db.ParkingLots.Add(lot);
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
        }

        await using var reloadDb = fixture.CreateDbContext();
        var reloaded = await reloadDb.Reservations.SingleAsync(r => r.Id == reservation.Id, ct);

        Assert.Equal(new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc), reloaded.StartTime);
        Assert.Equal(DateTimeKind.Utc, reloaded.StartTime.Kind);
        Assert.Equal(DateTimeKind.Utc, reloaded.EndTime.Kind);
        Assert.Equal(reservation.Interval, reloaded.Interval);
        Assert.Equal(new LicensePlate("A12K345"), reloaded.Plate);
        Assert.Equal(5.00m, reloaded.Price);
        Assert.Equal("BAM", reloaded.Currency);
        Assert.Equal(fixture.UserId, reloaded.UserId);
        Assert.Equal(space.Id, reloaded.SpaceId);
    }

    // I1: two requests load the same lot and add the same label. Each lot instance sees no duplicate,
    // so only the unique index can stop the second insert.
    [Fact]
    public async Task SameLabel_AddedConcurrentlyToOneLot_SecondSaveFailsWithUniqueViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = NewLot();

        await using (var db = fixture.CreateDbContext())
        {
            db.ParkingLots.Add(lot);
            await db.SaveChangesAsync(ct);
        }

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstLot = await first.ParkingLots.Include(l => l.Spaces).SingleAsync(l => l.Id == lot.Id, ct);
        var secondLot = await second.ParkingLots.Include(l => l.Spaces).SingleAsync(l => l.Id == lot.Id, ct);

        firstLot.AddSpace("B-1");
        secondLot.AddSpace("B-1");
        await first.SaveChangesAsync(ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(ct));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("ix_parking_spaces_lot_id_label", pg.ConstraintName);
    }
}

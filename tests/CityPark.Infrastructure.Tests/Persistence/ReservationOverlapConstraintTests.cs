using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class ReservationOverlapConstraintTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // 1 December 2026 is winter time (UTC+1): 10:00–13:00 UTC is 11:00–14:00 local, inside the lot's hours.
    private static readonly DateTimeOffset Day = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour) => Day.AddHours(hour);

    // Midnight UTC: before every test start and well within the booking horizon.
    private static FakeTimeProvider Clock => new(Day);

    [Fact]
    public async Task OverlappingActiveReservations_OnSameSpace_SecondInsertFailsWith23P01()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);
        await AddReservationAsync(lot, space, At(10), At(12), ReservationStatus.Active, ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddReservationAsync(lot, space, At(11), At(13), ReservationStatus.Active, ct));

        // Exception chain: DbUpdateException -> PostgresException (SQLSTATE 23P01, exclusion_violation).
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, pg.SqlState);
        Assert.Equal("23P01", pg.SqlState);
        Assert.Equal("ex_reservations_space_no_overlap", pg.ConstraintName);

        TestContext.Current.TestOutputHelper?.WriteLine(ex.ToString());
    }

    [Fact]
    public async Task TouchingIntervals_OnSameSpace_BothInsertsSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);

        await AddReservationAsync(lot, space, At(10), At(11), ReservationStatus.Active, ct);
        await AddReservationAsync(lot, space, At(11), At(12), ReservationStatus.Active, ct);

        Assert.Equal(2, await CountReservationsAsync(space.Id, ct));
    }

    [Fact]
    public async Task OverlappingCancelledReservation_OnSameSpace_DoesNotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);

        await AddReservationAsync(lot, space, At(10), At(12), ReservationStatus.Active, ct);
        await AddReservationAsync(lot, space, At(11), At(13), ReservationStatus.Cancelled, ct);

        Assert.Equal(2, await CountReservationsAsync(space.Id, ct));
    }

    [Fact]
    public async Task SameInterval_OnDifferentSpace_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (firstLot, first) = await AddSpaceAsync(ct);
        var (secondLot, second) = await AddSpaceAsync(ct);

        await AddReservationAsync(firstLot, first, At(10), At(12), ReservationStatus.Active, ct);
        await AddReservationAsync(secondLot, second, At(10), At(12), ReservationStatus.Active, ct);

        Assert.Equal(1, await CountReservationsAsync(second.Id, ct));
    }

    // R7: one car cannot be in two places at once, whichever lot or user books it.
    [Fact]
    public async Task OverlappingActiveReservations_ForSamePlate_InAnotherLot_SecondInsertFailsWith23P01()
    {
        var ct = TestContext.Current.CancellationToken;
        var plate = TestPlates.Next();
        var (firstLot, first) = await AddSpaceAsync(ct);
        var (secondLot, second) = await AddSpaceAsync(ct);
        await AddReservationAsync(firstLot, first, At(10), At(12), ReservationStatus.Active, ct, plate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddReservationAsync(secondLot, second, At(11), At(13), ReservationStatus.Active, ct, plate));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, pg.SqlState);
        Assert.Equal("ex_reservations_plate_no_overlap", pg.ConstraintName);
    }

    [Fact]
    public async Task TouchingIntervals_ForSamePlate_BothInsertsSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var plate = TestPlates.Next();
        var (firstLot, first) = await AddSpaceAsync(ct);
        var (secondLot, second) = await AddSpaceAsync(ct);

        await AddReservationAsync(firstLot, first, At(10), At(11), ReservationStatus.Active, ct, plate);
        await AddReservationAsync(secondLot, second, At(11), At(12), ReservationStatus.Active, ct, plate);

        Assert.Equal(1, await CountReservationsAsync(second.Id, ct));
    }

    [Fact]
    public async Task OverlappingCancelledReservation_ForSamePlate_DoesNotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var plate = TestPlates.Next();
        var (firstLot, first) = await AddSpaceAsync(ct);
        var (secondLot, second) = await AddSpaceAsync(ct);

        await AddReservationAsync(firstLot, first, At(10), At(12), ReservationStatus.Cancelled, ct, plate);
        await AddReservationAsync(secondLot, second, At(11), At(13), ReservationStatus.Active, ct, plate);

        Assert.Equal(1, await CountReservationsAsync(second.Id, ct));
    }

    private async Task<(ParkingLot Lot, ParkingSpace Space)> AddSpaceAsync(CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");
        db.ParkingLots.Add(lot); // Saving the lot also inserts its spaces.
        await db.SaveChangesAsync(ct);
        return (lot, space);
    }

    // A fresh DbContext per insert, like one request per context in the API: a failed
    // SaveChanges leaves the entity tracked, and reusing the context would retry it.
    private async Task AddReservationAsync(
        ParkingLot lot,
        ParkingSpace space,
        DateTimeOffset start,
        DateTimeOffset end,
        ReservationStatus status,
        CancellationToken ct,
        string? plate = null)
    {
        var reservation = Reservation.Create(
            lot, space, new TimeInterval(start, end), new LicensePlate(plate ?? TestPlates.Next()), fixture.UserId, Sarajevo, Clock);

        if (status == ReservationStatus.Cancelled)
        {
            reservation.Cancel(CancellationActor.Administrator, Clock);
        }

        await using var db = fixture.CreateDbContext();
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> CountReservationsAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.SpaceId == spaceId, ct);
    }
}

using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class ReservationOverlapConstraintTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime Day = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => Day.AddHours(hour);

    [Fact]
    public async Task OverlappingActiveReservations_OnSameSpace_SecondInsertFailsWith23P01()
    {
        var ct = TestContext.Current.CancellationToken;
        var space = await AddSpaceAsync(ct);
        await AddReservationAsync(space, At(10), At(12), ReservationStatus.Active, ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddReservationAsync(space, At(11), At(13), ReservationStatus.Active, ct));

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
        var space = await AddSpaceAsync(ct);

        await AddReservationAsync(space, At(10), At(11), ReservationStatus.Active, ct);
        await AddReservationAsync(space, At(11), At(12), ReservationStatus.Active, ct);

        Assert.Equal(2, await CountReservationsAsync(space, ct));
    }

    [Fact]
    public async Task OverlappingCancelledReservation_OnSameSpace_DoesNotFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var space = await AddSpaceAsync(ct);

        await AddReservationAsync(space, At(10), At(12), ReservationStatus.Active, ct);
        await AddReservationAsync(space, At(11), At(13), ReservationStatus.Cancelled, ct);

        Assert.Equal(2, await CountReservationsAsync(space, ct));
    }

    [Fact]
    public async Task SameInterval_OnDifferentSpace_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await AddSpaceAsync(ct);
        var second = await AddSpaceAsync(ct);

        await AddReservationAsync(first, At(10), At(12), ReservationStatus.Active, ct);
        await AddReservationAsync(second, At(10), At(12), ReservationStatus.Active, ct);

        Assert.Equal(1, await CountReservationsAsync(second, ct));
    }

    private async Task<Guid> AddSpaceAsync(CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        // Spaces are created through their lot. The lot itself is not mapped yet, so only the space is saved.
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");
        db.ParkingSpaces.Add(space);
        await db.SaveChangesAsync(ct);
        return space.Id;
    }

    // A fresh DbContext per insert, like one request per context in the API: a failed
    // SaveChanges leaves the entity tracked, and reusing the context would retry it.
    private async Task AddReservationAsync(
        Guid spaceId, DateTime start, DateTime end, ReservationStatus status, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        db.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            SpaceId = spaceId,
            StartTime = start,
            EndTime = end,
            Status = status,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> CountReservationsAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.SpaceId == spaceId, ct);
    }
}

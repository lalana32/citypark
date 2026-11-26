using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace CityPark.Infrastructure.Tests.Persistence;

public sealed class PostgresDatabaseErrorsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // 1 December 2026 is winter time (UTC+1): 10:00–12:00 UTC is 11:00–13:00 local, inside the lot's hours.
    private static readonly DateTimeOffset Day = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Clock => new(Day);

    private readonly PostgresDatabaseErrors _errors = new();

    [Fact]
    public async Task OverlappingActiveReservation_IsASpaceOverlapViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);
        await AddReservationAsync(lot, space, fixture.UserId, ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => AddReservationAsync(lot, space, fixture.UserId, ct));

        Assert.True(_errors.IsSpaceOverlapViolation(ex));
        Assert.False(_errors.IsPlateOverlapViolation(ex));
    }

    // Both overlap constraints raise 23P01; only the constraint name tells them apart.
    [Fact]
    public async Task SamePlateOverlappingInAnotherLot_IsAPlateOverlapViolation_NotASpaceOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var plate = TestPlates.Next();
        var (firstLot, first) = await AddSpaceAsync(ct);
        var (secondLot, second) = await AddSpaceAsync(ct);
        await AddReservationAsync(firstLot, first, fixture.UserId, ct, plate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddReservationAsync(secondLot, second, fixture.UserId, ct, plate));

        Assert.True(_errors.IsPlateOverlapViolation(ex));
        Assert.False(_errors.IsSpaceOverlapViolation(ex));
    }

    [Fact]
    public async Task ForeignKeyViolation_IsNotASpaceOverlapViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);

        // No Identity user has this id: PostgreSQL answers 23503 (foreign_key_violation), not 23P01.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddReservationAsync(lot, space, Guid.CreateVersion7(), ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
        Assert.False(_errors.IsSpaceOverlapViolation(ex));
        Assert.False(_errors.IsPlateOverlapViolation(ex));
    }

    [Fact]
    public void DbUpdateException_WithoutPostgresCause_IsNoOverlapViolation()
    {
        var ex = new DbUpdateException("save failed");

        Assert.False(_errors.IsSpaceOverlapViolation(ex));
        Assert.False(_errors.IsPlateOverlapViolation(ex));
    }

    [Fact]
    public async Task SameLabelAddedByTwoContexts_IsASpaceLabelViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, _) = await AddSpaceAsync(ct);

        // Both load the lot before either saves, as two parallel requests would: the domain check passes in both.
        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstLot = await first.ParkingLots.Include(l => l.Spaces).SingleAsync(l => l.Id == lot.Id, ct);
        var secondLot = await second.ParkingLots.Include(l => l.Spaces).SingleAsync(l => l.Id == lot.Id, ct);
        firstLot.AddSpace("B-1");
        secondLot.AddSpace("B-1");
        await first.SaveChangesAsync(ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(ct));

        Assert.True(_errors.IsSpaceLabelViolation(ex));
        Assert.False(_errors.IsSpaceOverlapViolation(ex));
    }

    [Fact]
    public async Task OverlappingActiveReservation_IsNotASpaceLabelViolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddSpaceAsync(ct);
        await AddReservationAsync(lot, space, fixture.UserId, ct);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => AddReservationAsync(lot, space, fixture.UserId, ct));

        Assert.False(_errors.IsSpaceLabelViolation(ex));
    }

    [Fact]
    public void UniqueViolation_OnAnotherIndex_IsNotASpaceLabelViolation()
    {
        var pg = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: "ix_users_normalized_email");

        Assert.False(_errors.IsSpaceLabelViolation(new DbUpdateException("save failed", pg)));
    }

    private async Task<(ParkingLot Lot, ParkingSpace Space)> AddSpaceAsync(CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return (lot, space);
    }

    private async Task AddReservationAsync(
        ParkingLot lot, ParkingSpace space, Guid userId, CancellationToken ct, string? plate = null)
    {
        var interval = new TimeInterval(Day.AddHours(10), Day.AddHours(12));
        var reservation = Reservation.Create(
            lot, space, interval, new LicensePlate(plate ?? TestPlates.Next()), userId, Sarajevo, Clock);

        await using var db = fixture.CreateDbContext();
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
    }
}

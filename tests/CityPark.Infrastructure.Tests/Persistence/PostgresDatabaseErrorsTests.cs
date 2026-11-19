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
    }

    [Fact]
    public void ExclusionViolation_OnAnotherConstraint_IsNotASpaceOverlapViolation()
    {
        // The future plate constraint (R7) raises 23P01 too; only the name tells them apart.
        var pg = new PostgresException(
            "conflicting key value violates exclusion constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.ExclusionViolation,
            constraintName: "ex_reservations_plate_no_overlap");

        Assert.False(_errors.IsSpaceOverlapViolation(new DbUpdateException("save failed", pg)));
    }

    [Fact]
    public void DbUpdateException_WithoutPostgresCause_IsNotASpaceOverlapViolation()
    {
        Assert.False(_errors.IsSpaceOverlapViolation(new DbUpdateException("save failed")));
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

    private async Task AddReservationAsync(ParkingLot lot, ParkingSpace space, Guid userId, CancellationToken ct)
    {
        var interval = new TimeInterval(Day.AddHours(10), Day.AddHours(12));
        var reservation = Reservation.Create(lot, space, interval, new LicensePlate("A12K345"), userId, Sarajevo, Clock);

        await using var db = fixture.CreateDbContext();
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
    }
}

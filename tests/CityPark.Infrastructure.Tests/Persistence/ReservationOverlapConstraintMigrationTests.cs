using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Persistence;

// Checks the AddReservationOverlapConstraint migration itself: that it creates the constraint, that
// rolling it back removes only the constraint, and that it applies to a table that already has data.
// Whether the constraint rejects overlaps is covered by ReservationOverlapConstraintTests.
public sealed class ReservationOverlapConstraintMigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string InitialCreate = "InitialCreate";

    // contype is PostgreSQL's one-byte "char" type; ::text reads it as a string. EF Core expects a
    // single scalar column to be named "Value".
    private const string ConstraintTypesSql =
        """SELECT contype::text AS "Value" FROM pg_constraint WHERE conname = 'ex_reservations_space_no_overlap'""";

    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // 1 December 2026 is winter time (UTC+1): 10:00–13:00 UTC is 11:00–14:00 local, inside the lot's hours.
    private static readonly DateTimeOffset Day = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour) => Day.AddHours(hour);

    private static FakeTimeProvider Clock => new(Day);

    [Fact]
    public async Task Migrate_CreatesExclusionConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext();

        // contype 'x' = exclusion constraint ('p' primary key, 'f' foreign key, 'u' unique, 'c' check).
        Assert.Equal("x", Assert.Single(await ConstraintTypesAsync(db, ct)));
    }

    [Fact]
    public async Task RollingBackTheMigration_RemovesOnlyTheConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext(NewDatabaseName());
        await db.Database.MigrateAsync(ct);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(InitialCreate, cancellationToken: ct);

        Assert.Empty(await ConstraintTypesAsync(db, ct));
        Assert.Equal(0, await db.Reservations.CountAsync(ct)); // The table is still there.

        // Up again after Down: the migration can be applied a second time.
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Equal("x", Assert.Single(await ConstraintTypesAsync(db, ct)));
    }

    [Fact]
    public async Task Migrate_OnTableWithNonOverlappingReservations_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = NewDatabaseName();
        var userId = Guid.CreateVersion7();

        await using (var db = fixture.CreateDbContext(database))
        {
            await db.GetService<IMigrator>().MigrateAsync(InitialCreate, cancellationToken: ct);
        }

        // Rows written while the schema has no constraint yet, in separate contexts as in the API.
        var (lot, space) = await AddSpaceAsync(database, userId, ct);
        await AddReservationAsync(database, lot, space, userId, At(10), At(11), ReservationStatus.Active, ct);
        await AddReservationAsync(database, lot, space, userId, At(11), At(12), ReservationStatus.Active, ct); // touches, [)
        await AddReservationAsync(database, lot, space, userId, At(10), At(12), ReservationStatus.Cancelled, ct); // ignored by WHERE

        await using var migrated = fixture.CreateDbContext(database);
        await migrated.Database.MigrateAsync(ct);

        Assert.Equal("x", Assert.Single(await ConstraintTypesAsync(migrated, ct)));
        Assert.Equal(3, await migrated.Reservations.CountAsync(r => r.SpaceId == space.Id, ct));
    }

    private static string NewDatabaseName() => $"migration_{Guid.CreateVersion7():N}";

    private static Task<List<string>> ConstraintTypesAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.SqlQueryRaw<string>(ConstraintTypesSql).ToListAsync(ct);

    private async Task<(ParkingLot Lot, ParkingSpace Space)> AddSpaceAsync(
        string database, Guid userId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext(database);
        db.Users.Add(new IdentityUser<Guid> { Id = userId, UserName = "migration-driver" });

        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");
        db.ParkingLots.Add(lot);

        await db.SaveChangesAsync(ct);
        return (lot, space);
    }

    private async Task AddReservationAsync(
        string database,
        ParkingLot lot,
        ParkingSpace space,
        Guid userId,
        DateTimeOffset start,
        DateTimeOffset end,
        ReservationStatus status,
        CancellationToken ct)
    {
        var reservation = Reservation.Create(
            lot, space, new TimeInterval(start, end), new LicensePlate("A12K345"), userId, Sarajevo, Clock);

        if (status == ReservationStatus.Cancelled)
        {
            reservation.Cancel(CancellationActor.Administrator, Clock);
        }

        await using var db = fixture.CreateDbContext(database);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
    }
}

using CityPark.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CityPark.Infrastructure.Tests.Persistence;

// Checks the AddReservationPlateOverlapConstraint migration itself. Whether the constraint rejects
// overlaps is covered by ReservationOverlapConstraintTests.
public sealed class ReservationPlateOverlapConstraintMigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string PreviousMigration = "MakeParkingSpaceLotIdRequired";

    private const string ConstraintsSql =
        """
        SELECT conname::text AS "Value" FROM pg_constraint
        WHERE contype = 'x' AND conrelid = 'reservations'::regclass
        ORDER BY conname
        """;

    [Fact]
    public async Task Migrate_CreatesBothExclusionConstraints()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext();

        Assert.Equal(
            ["ex_reservations_plate_no_overlap", "ex_reservations_space_no_overlap"],
            await ExclusionConstraintsAsync(db, ct));
    }

    [Fact]
    public async Task RollingBackTheMigration_RemovesOnlyThePlateConstraint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext($"migration_{Guid.CreateVersion7():N}");
        await db.Database.MigrateAsync(ct);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration, cancellationToken: ct);

        Assert.Equal(["ex_reservations_space_no_overlap"], await ExclusionConstraintsAsync(db, ct));

        // Up again after Down: the migration can be applied a second time.
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Equal(2, (await ExclusionConstraintsAsync(db, ct)).Count);
    }

    private static Task<List<string>> ExclusionConstraintsAsync(AppDbContext db, CancellationToken ct) =>
        db.Database.SqlQueryRaw<string>(ConstraintsSql).ToListAsync(ct);
}

using CityPark.Application.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CityPark.Infrastructure.Persistence;

public sealed class PostgresDatabaseErrors : IDatabaseErrors
{
    // Also spelled out in their migrations on purpose: a migration must never change after it is applied.
    public const string SpaceOverlapConstraint = "ex_reservations_space_no_overlap";
    public const string PlateOverlapConstraint = "ex_reservations_plate_no_overlap";

    // Named by the snake_case convention in InitialCreate, not in ParkingSpaceConfiguration.
    public const string SpaceLabelIndex = "ix_parking_spaces_lot_id_label";

    // Both overlap constraints raise 23P01; only the name tells them apart.
    public bool IsSpaceOverlapViolation(DbUpdateException exception) =>
        IsViolation(exception, PostgresErrorCodes.ExclusionViolation, SpaceOverlapConstraint);

    public bool IsPlateOverlapViolation(DbUpdateException exception) =>
        IsViolation(exception, PostgresErrorCodes.ExclusionViolation, PlateOverlapConstraint);

    public bool IsSpaceLabelViolation(DbUpdateException exception) =>
        IsViolation(exception, PostgresErrorCodes.UniqueViolation, SpaceLabelIndex);

    private static bool IsViolation(DbUpdateException exception, string sqlState, string constraintName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is PostgresException pg
            && pg.SqlState == sqlState
            && pg.ConstraintName == constraintName;
    }
}

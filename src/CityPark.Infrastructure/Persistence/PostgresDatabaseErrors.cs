using CityPark.Application.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CityPark.Infrastructure.Persistence;

public sealed class PostgresDatabaseErrors : IDatabaseErrors
{
    // Also spelled out in its migration on purpose: a migration must never change after it is applied.
    public const string SpaceOverlapConstraint = "ex_reservations_space_no_overlap";

    // Named by the snake_case convention in InitialCreate, not in ParkingSpaceConfiguration.
    public const string SpaceLabelIndex = "ix_parking_spaces_lot_id_label";

    public bool IsSpaceOverlapViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // The name matters: the future plate constraint (R7) raises 23P01 too.
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
            ConstraintName: SpaceOverlapConstraint,
        };
    }

    public bool IsSpaceLabelViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: SpaceLabelIndex,
        };
    }
}

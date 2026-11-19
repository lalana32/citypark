using CityPark.Application.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CityPark.Infrastructure.Persistence;

public sealed class PostgresDatabaseErrors : IDatabaseErrors
{
    // Also spelled out in its migration on purpose: a migration must never change after it is applied.
    public const string SpaceOverlapConstraint = "ex_reservations_space_no_overlap";

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
}

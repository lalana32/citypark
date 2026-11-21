using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Persistence;

/// <summary>Reads database errors for use cases, so Application never sees provider exception types.</summary>
public interface IDatabaseErrors
{
    /// <summary>True only for the space no-overlap constraint (23P01). Any other error must be rethrown.</summary>
    bool IsSpaceOverlapViolation(DbUpdateException exception);

    /// <summary>True only for the unique space label within a lot (23505, I1). Any other error must be rethrown.</summary>
    bool IsSpaceLabelViolation(DbUpdateException exception);
}

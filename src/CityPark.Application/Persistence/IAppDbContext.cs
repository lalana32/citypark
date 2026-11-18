using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Persistence;

/// <summary>
/// The unit of work that use cases depend on. Implemented by AppDbContext in Infrastructure,
/// so Application knows EF Core's query API but not which database is behind it.
/// </summary>
public interface IAppDbContext
{
    DbSet<Reservation> Reservations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

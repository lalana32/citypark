using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Persistence;

/// <summary>
/// The unit of work that use cases depend on. Implemented by AppDbContext in Infrastructure,
/// so Application knows EF Core's query API but not which database is behind it.
/// </summary>
public interface IAppDbContext
{
    /// <summary>Lots with their spaces. Spaces have no set of their own: they change only through the lot.</summary>
    DbSet<ParkingLot> ParkingLots { get; }

    DbSet<Reservation> Reservations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

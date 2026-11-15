using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Persistence;

public sealed class CityParkDbContext(DbContextOptions<CityParkDbContext> options) : DbContext(options)
{
    public DbSet<ParkingSpace> ParkingSpaces => Set<ParkingSpace>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CityParkDbContext).Assembly);
}

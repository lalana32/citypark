using Microsoft.EntityFrameworkCore.Design;

namespace CityPark.Infrastructure.Persistence;

internal sealed class CityParkDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CityParkDbContext>
{
    private const string LocalDevelopment =
        "Host=localhost;Port=5432;Database=citypark;Username=citypark;Password=citypark";

    public CityParkDbContext CreateDbContext(string[] args) =>
        new(CityParkDbContextOptions.Create(
            Environment.GetEnvironmentVariable("CITYPARK_DB") ?? LocalDevelopment));
}

using Microsoft.EntityFrameworkCore.Design;

namespace CityPark.Infrastructure.Persistence;

internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDevelopment =
        "Host=localhost;Port=5432;Database=citypark;Username=citypark;Password=citypark";

    public AppDbContext CreateDbContext(string[] args) =>
        new(AppDbContextOptions.Create(
            Environment.GetEnvironmentVariable("CITYPARK_DB") ?? LocalDevelopment));
}

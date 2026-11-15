using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Persistence;

public static class CityParkDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }

    public static DbContextOptions<CityParkDbContext> Create(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<CityParkDbContext>();
        Configure(builder, connectionString);
        return builder.Options;
    }
}

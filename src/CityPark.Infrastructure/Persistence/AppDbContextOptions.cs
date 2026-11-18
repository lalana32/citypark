using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Persistence;

public static class AppDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }

    public static DbContextOptions<AppDbContext> Create(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder, connectionString);
        return builder.Options;
    }
}

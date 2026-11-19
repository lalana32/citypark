using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Persistence;

public static class AppDbContextOptions
{
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            // Retries transient errors such as the deadlock (40P01) parallel inserts can hit on the
            // exclusion constraint (CP-017, ADR-002).
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention();
    }

    public static DbContextOptions<AppDbContext> Create(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder, connectionString);
        return builder.Options;
    }
}

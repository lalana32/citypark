using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CityPark.Infrastructure.Persistence.Seeding;

/// <summary>
/// Demo lots for a fresh local database. Runs in the Development environment, or elsewhere when
/// <see cref="EnabledKey"/> is set (the local Docker stack). Safe to run on every start: a lot whose
/// name is already in the database is skipped.
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string EnabledKey = "DemoData:Enabled";

    private static readonly DemoLot[] Lots =
    [
        new("Centar", "Ferhadija 1", new Rate(2.50m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)), 30),
        new("Marijin Dvor", "Zmaja od Bosne 8", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(6, 0), new TimeOnly(23, 0)), 20),
        new("Baščaršija", "Bravadžiluk 2", new Rate(3.00m, "BAM"), new OpeningHours(new TimeOnly(8, 0), new TimeOnly(20, 0)), 12),
    ];

    /// <summary>
    /// Adds the demo lots that are missing. Outside Development it does nothing unless
    /// <paramref name="enabledOutsideDevelopment"/> is true, so a real Production database never
    /// receives demo data. The database must already be migrated.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(
        this IServiceProvider services,
        IHostEnvironment environment,
        bool enabledOutsideDevelopment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment() && !enabledOutsideDevelopment)
        {
            return;
        }

        // AppDbContext is scoped; at startup there is no request scope, so the seeder makes its own.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingNames = await db.ParkingLots.Select(l => l.Name).ToListAsync(cancellationToken);

        foreach (var demo in Lots.Where(l => !existingNames.Contains(l.Name)))
        {
            db.ParkingLots.Add(demo.ToParkingLot());
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record DemoLot(string Name, string Address, Rate Rate, OpeningHours OpeningHours, int SpaceCount)
    {
        public ParkingLot ToParkingLot()
        {
            var lot = ParkingLot.Create(Name, Address, Rate, OpeningHours);

            // Zero-padded labels (A-01 ... A-30) sort in the same order as their numbers.
            for (var number = 1; number <= SpaceCount; number++)
            {
                lot.AddSpace($"A-{number:D2}");
            }

            return lot;
        }
    }
}

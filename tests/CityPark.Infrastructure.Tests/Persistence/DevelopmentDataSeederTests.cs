using CityPark.Domain.Lots;
using CityPark.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CityPark.Infrastructure.Tests.Persistence;

// Each test seeds its own freshly migrated database, so the counts are not affected by rows
// that other tests in this class add.
public sealed class DevelopmentDataSeederTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Seed_RunTwiceInDevelopment_ProducesTheSameRowCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await ProviderForNewDatabaseAsync("seed_twice", ct);

        await provider.SeedDevelopmentDataAsync(new TestHostEnvironment(Environments.Development), ct);
        var afterFirstRun = await CountRowsAsync("seed_twice", ct);

        await provider.SeedDevelopmentDataAsync(new TestHostEnvironment(Environments.Development), ct);
        var afterSecondRun = await CountRowsAsync("seed_twice", ct);

        Assert.Equal((Lots: 3, Spaces: 62), afterFirstRun);
        Assert.Equal(afterFirstRun, afterSecondRun);
    }

    [Fact]
    public async Task Seed_InDevelopment_SavesThreeLotsWithDistinctRatesAndValidWindows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await ProviderForNewDatabaseAsync("seed_values", ct);

        await provider.SeedDevelopmentDataAsync(new TestHostEnvironment(Environments.Development), ct);

        await using var db = fixture.CreateDbContext("seed_values");
        var lots = await db.ParkingLots.Include(l => l.Spaces).OrderBy(l => l.Name).ToListAsync(ct);

        Assert.Equal(
            [("Baščaršija", 12), ("Centar", 30), ("Marijin Dvor", 20)],
            lots.Select(l => (l.Name, l.Spaces.Count)));
        Assert.Equal(lots.Count, lots.Select(l => l.Rate.HourlyPrice).Distinct().Count());

        foreach (var lot in lots)
        {
            // Rebuilding the value objects from the stored columns runs their invariants again:
            // a positive two-decimal price, an ISO currency and a window that closes after it opens.
            Assert.Equal(lot.Rate, new Rate(lot.Rate.HourlyPrice, lot.Rate.Currency));
            Assert.Equal(lot.OpeningHours, new OpeningHours(lot.OpeningHours.OpensAt, lot.OpeningHours.ClosesAt));
            Assert.All(lot.Spaces, s => Assert.True(s.IsActive));
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Seed_OutsideDevelopment_AddsNothing(string environmentName)
    {
        var ct = TestContext.Current.CancellationToken;
        var database = $"seed_{environmentName.ToLowerInvariant()}";
        await using var provider = await ProviderForNewDatabaseAsync(database, ct);

        await provider.SeedDevelopmentDataAsync(new TestHostEnvironment(environmentName), ct);

        Assert.Equal((Lots: 0, Spaces: 0), await CountRowsAsync(database, ct));
    }

    // Same registration as the API (AddInfrastructure), pointed at a new database migrated to the latest schema.
    private async Task<ServiceProvider> ProviderForNewDatabaseAsync(string database, CancellationToken ct)
    {
        await using (var db = fixture.CreateDbContext(database))
        {
            await db.Database.MigrateAsync(ct);
        }

        return new ServiceCollection()
            .AddInfrastructure(fixture.GetConnectionString(database))
            .BuildServiceProvider(validateScopes: true);
    }

    private async Task<(int Lots, int Spaces)> CountRowsAsync(string database, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext(database);
        return (await db.ParkingLots.CountAsync(ct), await db.Set<ParkingSpace>().CountAsync(ct));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "CityPark.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

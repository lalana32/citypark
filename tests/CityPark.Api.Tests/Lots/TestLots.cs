using CityPark.Domain.Lots;
using CityPark.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace CityPark.Api.Tests.Lots;

/// <summary>Lots written straight to the test database: the demo seeder runs only in Development.</summary>
internal static class TestLots
{
    public static readonly OpeningHours DayHours = new(new TimeOnly(7, 0), new TimeOnly(22, 0));

    public static Task<ParkingLot> AddAsync(
        ApiFactory factory,
        string name,
        int spaces,
        int inactiveSpaces,
        CancellationToken ct) =>
        AddAsync(factory, name, spaces, inactiveSpaces, new Rate(2.50m, "BAM"), ct);

    public static async Task<ParkingLot> AddAsync(
        ApiFactory factory,
        string name,
        int spaces,
        int inactiveSpaces,
        Rate rate,
        CancellationToken ct)
    {
        var lot = ParkingLot.Create(name, "Ferhadija 1", rate, DayHours);

        for (var number = 1; number <= spaces; number++)
        {
            var space = lot.AddSpace($"A-{number:D2}");

            if (number <= inactiveSpaces)
            {
                space.Deactivate();
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);

        return lot;
    }

    /// <summary>A name no other test uses, so tests sharing one database cannot see each other's lots.</summary>
    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";
}

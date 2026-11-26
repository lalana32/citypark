using CityPark.Application.Reservations;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Tests.Concurrency;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Reservations;

// Each round uses a new lot, so rounds and tests cannot see each other's reservations.
public sealed class ParallelReservationTests : IClassFixture<PostgresFixture>, IAsyncDisposable
{
    private const int Rounds = 20;
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private readonly PostgresFixture _fixture;
    private readonly ServiceProvider _services;

    public ParallelReservationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _services = new ServiceCollection()
            .AddInfrastructure(fixture.ConnectionString)
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Local(8)))
            .AddSingleton(Sarajevo)
            .AddScoped<CreateReservationService>()
            .BuildServiceProvider(validateScopes: true);
    }

    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    [Fact]
    public async Task ScenarioA_IdenticalIntervals_OnSameSpace_ExactlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            // A new user per round, so the rounds cannot add up to the R10 limit.
            var user = await _fixture.AddUserAsync();
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(20, _ => CreateAsync(user, space, Local(10), Local(11), ct));

            var successes = CountSuccesses(round, results);
            Assert.True(successes == 1, $"Round {round}: {successes} of 20 succeeded, expected 1.");
            Assert.True(await CountActiveAsync(space, ct) == 1, $"Round {round}: expected 1 active row.");
        }
    }

    [Fact]
    public async Task ScenarioB_StaggeredOverlappingIntervals_OnSameSpace_LeaveNoOverlappingActiveRows()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var user = await _fixture.AddUserAsync();
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(20, i =>
            {
                var start = Local(10).AddMinutes(15 * i);
                return CreateAsync(user, space, start, start.AddHours(1), ct);
            });

            var successes = CountSuccesses(round, results);
            var active = await ActiveIntervalsAsync(space, ct);
            Assert.True(successes >= 1, $"Round {round}: no request succeeded.");
            Assert.True(active.Count == successes, $"Round {round}: {active.Count} active rows, {successes} successes.");

            for (var a = 0; a < active.Count; a++)
            {
                for (var b = a + 1; b < active.Count; b++)
                {
                    Assert.False(active[a].Overlaps(active[b]), $"Round {round}: {active[a]} overlaps {active[b]}.");
                }
            }
        }
    }

    [Fact]
    public async Task ScenarioC_SameInterval_RoundRobinOverFiveSpaces_OnePerSpaceSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var user = await _fixture.AddUserAsync();
            var spaces = await AddLotAsync(5, ct);

            var results = await ParallelRunner.RunAsync(20, i => CreateAsync(user, spaces[i % 5], Local(10), Local(11), ct));

            var successes = CountSuccesses(round, results);
            Assert.True(successes == 5, $"Round {round}: {successes} of 20 succeeded, expected 5.");

            foreach (var space in spaces)
            {
                Assert.True(await CountActiveAsync(space, ct) == 1, $"Round {round}: space {space} must have 1 active row.");
            }
        }
    }

    [Fact]
    public async Task ScenarioD_AdjacentIntervals_OnSameSpace_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var user = await _fixture.AddUserAsync();
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(10, i =>
            {
                var start = Local(10).AddMinutes(30 * i);
                return CreateAsync(user, space, start, start.AddMinutes(30), ct);
            });

            var successes = CountSuccesses(round, results);
            Assert.True(successes == 10, $"Round {round}: {successes} of 10 succeeded, expected 10.");
            Assert.True(await CountActiveAsync(space, ct) == 10, $"Round {round}: expected 10 active rows.");
        }
    }

    [Fact]
    public async Task ScenarioE_SameInterval_ByLot_WithFiveSpaces_ExactlyFiveSucceed()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var user = await _fixture.AddUserAsync();
            var lot = await AddLotEntityAsync(5, ct);

            // Every request sees the same five candidates, so most lose the first insert and must retry.
            var results = await ParallelRunner.RunAsync(20, _ => CreateInLotAsync(user, lot.Id, Local(10), Local(11), ct));

            var successes = CountSuccesses<NoFreeSpaceException>(round, results);
            Assert.True(successes == 5, $"Round {round}: {successes} of 20 succeeded, expected 5.");

            foreach (var space in lot.Spaces)
            {
                Assert.True(await CountActiveAsync(space.Id, ct) == 1, $"Round {round}: space {space.Label} must have 1 active row.");
            }
        }
    }

    // R7: twenty free spaces in twenty lots, one car. No space is contested, only the plate.
    [Fact]
    public async Task ScenarioF_SamePlateAndInterval_InTwentyLots_ExactlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var user = await _fixture.AddUserAsync();
            var plate = TestPlates.Next();
            var spaces = new List<Guid>();
            for (var n = 0; n < 20; n++)
            {
                spaces.Add((await AddLotAsync(1, ct))[0]);
            }

            var results = await ParallelRunner.RunAsync(20, i => CreateAsync(user, spaces[i], Local(10), Local(11), ct, plate));

            var successes = CountSuccesses<PlateOverlapException>(round, results);
            Assert.True(successes == 1, $"Round {round}: {successes} of 20 succeeded, expected 1.");
            Assert.True(await CountActiveAsync(plate, ct) == 1, $"Round {round}: expected 1 active row for {plate}.");
        }
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private static int CountSuccesses(int round, IReadOnlyList<Exception?> results) =>
        CountSuccesses<ReservationConflictException>(round, results);

    // Any failure other than the expected conflict fails the test straight away.
    private static int CountSuccesses<TConflict>(int round, IReadOnlyList<Exception?> results)
        where TConflict : Exception
    {
        var unexpected = results.FirstOrDefault(r => r is not null and not TConflict);
        Assert.True(unexpected is null, $"Round {round}: unexpected failure {unexpected}");
        return results.Count(r => r is null);
    }

    private async Task CreateInLotAsync(Guid userId, Guid lotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CreateReservationService>();
        await service.CreateInLotAsync(userId, lotId, start, end, TestPlates.Next(), ct);
    }

    private async Task CreateAsync(
        Guid userId, Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct, string? plate = null)
    {
        await using var scope = _services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CreateReservationService>();
        await service.CreateAsync(userId, spaceId, start, end, plate ?? TestPlates.Next(), ct);
    }

    private async Task<int> CountActiveAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.SpaceId == spaceId && r.Status == ReservationStatus.Active, ct);
    }

    private async Task<int> CountActiveAsync(string plate, CancellationToken ct)
    {
        var licensePlate = new LicensePlate(plate);
        await using var db = _fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.Plate == licensePlate && r.Status == ReservationStatus.Active, ct);
    }

    private async Task<List<TimeInterval>> ActiveIntervalsAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = _fixture.CreateDbContext();
        var rows = await db.Reservations
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.Status == ReservationStatus.Active)
            .ToListAsync(ct);
        return [.. rows.Select(r => r.Interval)];
    }

    private async Task<IReadOnlyList<Guid>> AddLotAsync(int spaceCount, CancellationToken ct) =>
        [.. (await AddLotEntityAsync(spaceCount, ct)).Spaces.Select(s => s.Id)];

    private async Task<ParkingLot> AddLotEntityAsync(int spaceCount, CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));

        for (var n = 1; n <= spaceCount; n++)
        {
            lot.AddSpace($"A-{n}");
        }

        await using var db = _fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return lot;
    }
}

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
    private const string Plate = "A12K345";

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
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(20, _ => CreateAsync(space, Local(10), Local(11), ct));

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
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(20, i =>
            {
                var start = Local(10).AddMinutes(15 * i);
                return CreateAsync(space, start, start.AddHours(1), ct);
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
            var spaces = await AddLotAsync(5, ct);

            var results = await ParallelRunner.RunAsync(20, i => CreateAsync(spaces[i % 5], Local(10), Local(11), ct));

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
            var space = (await AddLotAsync(1, ct))[0];

            var results = await ParallelRunner.RunAsync(10, i =>
            {
                var start = Local(10).AddMinutes(30 * i);
                return CreateAsync(space, start, start.AddMinutes(30), ct);
            });

            var successes = CountSuccesses(round, results);
            Assert.True(successes == 10, $"Round {round}: {successes} of 10 succeeded, expected 10.");
            Assert.True(await CountActiveAsync(space, ct) == 10, $"Round {round}: expected 10 active rows.");
        }
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    // Any failure other than a conflict fails the test straight away.
    private static int CountSuccesses(int round, IReadOnlyList<Exception?> results)
    {
        var unexpected = results.FirstOrDefault(r => r is not null and not ReservationConflictException);
        Assert.True(unexpected is null, $"Round {round}: unexpected failure {unexpected}");
        return results.Count(r => r is null);
    }

    private async Task CreateAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CreateReservationService>();
        await service.CreateAsync(_fixture.UserId, spaceId, start, end, Plate, ct);
    }

    private async Task<int> CountActiveAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.SpaceId == spaceId && r.Status == ReservationStatus.Active, ct);
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

    private async Task<IReadOnlyList<Guid>> AddLotAsync(int spaceCount, CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));

        var spaces = Enumerable.Range(1, spaceCount).Select(n => lot.AddSpace($"A-{n}").Id).ToList();

        await using var db = _fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return spaces;
    }
}

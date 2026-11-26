using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;
using CityPark.Infrastructure.Tests.Concurrency;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Reservations;

// Each test and round books its own 10:00–11:00 reservation on a new lot.
public sealed class ParallelCancelReservationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const int Rounds = 20;

    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    private Actor Owner => new(fixture.UserId, CancellationActor.Customer);

    [Fact]
    public async Task StaleCopy_SavedAfterAnotherCancel_ThrowsConcurrencyException()
    {
        var ct = TestContext.Current.CancellationToken;
        var reservationId = await BookTenToElevenAsync(ct);

        // Both "requests" read the row while it is still Active.
        await using var stale = fixture.CreateDbContext();
        var staleCopy = await stale.Reservations.SingleAsync(r => r.Id == reservationId, ct);

        await CancelAsync(reservationId, Owner, ct);

        staleCopy.Cancel(CancellationActor.Customer, new FakeTimeProvider(Local(9, 40)));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task SimultaneousCancels_ExactlyOneSucceedsOthersGetConflict()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 1; round <= Rounds; round++)
        {
            var reservationId = await BookTenToElevenAsync(ct);

            var results = await ParallelRunner.RunAsync(20, _ => CancelAsync(reservationId, Owner, ct));

            // A late request may read Cancelled (domain conflict) or a stale Active (token conflict); both are 409.
            var unexpected = results.FirstOrDefault(r => r is not null and not DomainConflictException);
            Assert.True(unexpected is null, $"Round {round}: unexpected failure {unexpected}");

            var successes = results.Count(r => r is null);
            Assert.True(successes == 1, $"Round {round}: {successes} of 20 succeeded, expected 1.");
        }
    }

    private async Task CancelAsync(Guid reservationId, Actor actor, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CancelReservationService(db, new FakeTimeProvider(Local(9, 40)));
        await service.CancelAsync(reservationId, actor, ct);
    }

    private async Task<Guid> BookTenToElevenAsync(CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);

        var service = new CreateReservationService(db, new PostgresDatabaseErrors(), new FakeTimeProvider(Local(8)), Sarajevo);
        var reservation = await service.CreateAsync(fixture.UserId, space.Id, Local(10), Local(11), TestPlates.Next(), ct);
        return reservation.Id;
    }
}

using CityPark.Application.Admin;
using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Admin;

// Each test creates its own lot. Reservations are 10:00–11:00, booked at 08:00.
public sealed class AdminLotServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static readonly LotDetails Centar = new("Centar", "Ferhadija 1", 2.00m, "BAM", new TimeOnly(7, 0), new TimeOnly(22, 0));

    // Tuesday 1 December 2026, winter time in Sarajevo (UTC+1). Times in the tests are local.
    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    [Fact]
    public async Task CreateLot_StoresTheLot()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await RunAsync(Local(8), s => s.CreateLotAsync(Centar, ct));

        var stored = await LotAsync(created.Id, ct);
        Assert.Equal("Centar", stored.Name);
        Assert.Equal(new Rate(2.00m, "BAM"), stored.Rate);
        Assert.Equal(new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)), stored.OpeningHours);
    }

    [Fact]
    public async Task CreateLot_WithClosingBeforeOpening_ThrowsDomainExceptionForClosesAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var invalid = Centar with { Name = $"Invalid {Guid.NewGuid():N}", OpensAt = new TimeOnly(22, 0), ClosesAt = new TimeOnly(7, 0) };

        var ex = await Assert.ThrowsAsync<DomainException>(() => RunAsync(Local(8), s => s.CreateLotAsync(invalid, ct)));

        Assert.Equal(nameof(OpeningHours.ClosesAt), ex.Field);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.ParkingLots.AnyAsync(l => l.Name == invalid.Name, ct));
    }

    [Fact]
    public async Task UpdateLot_ChangesTheRateButNotTheStoredPriceOfAnExistingReservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddLotWithSpaceAsync(ct);
        var reservation = await BookTenToElevenAsync(space.Id, ct);

        await RunAsync(Local(8, 30), s => s.UpdateLotAsync(lot.Id, Centar with { HourlyPrice = 3.00m }, ct));

        Assert.Equal(3.00m, (await LotAsync(lot.Id, ct)).Rate.HourlyPrice);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(2.00m, await db.Reservations.Where(r => r.Id == reservation.Id).Select(r => r.Price).SingleAsync(ct));
    }

    [Fact]
    public async Task UpdateLot_UnknownLot_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            RunAsync(Local(8), s => s.UpdateLotAsync(Guid.CreateVersion7(), Centar, ct)));
    }

    [Fact]
    public async Task AddSpace_StoresAnActiveSpaceInTheLot()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, _) = await AddLotWithSpaceAsync(ct);

        var space = await RunAsync(Local(8), s => s.AddSpaceAsync(lot.Id, " b-1 ", ct));

        var stored = await LotAsync(lot.Id, ct);
        Assert.Contains(stored.Spaces, s => s.Id == space.Id && s.Label == "B-1" && s.IsActive);
    }

    [Fact]
    public async Task AddSpace_DuplicateLabel_ThrowsConflictAndAddsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, _) = await AddLotWithSpaceAsync(ct);

        await Assert.ThrowsAsync<DomainConflictException>(() => RunAsync(Local(8), s => s.AddSpaceAsync(lot.Id, "a-1", ct)));

        Assert.Single((await LotAsync(lot.Id, ct)).Spaces);
    }

    [Fact]
    public async Task AddSpace_UnknownLot_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            RunAsync(Local(8), s => s.AddSpaceAsync(Guid.CreateVersion7(), "A-1", ct)));
    }

    [Theory]
    [InlineData(9, 0)] // before the reservation starts
    [InlineData(10, 30)] // while it is in progress
    public async Task DeactivateSpace_WithActiveReservationNotEnded_ThrowsConflictListingItAndStaysActive(int hour, int minute)
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddLotWithSpaceAsync(ct);
        var reservation = await BookTenToElevenAsync(space.Id, ct);

        var ex = await Assert.ThrowsAsync<SpaceHasActiveReservationsException>(() =>
            RunAsync(Local(hour, minute), s => s.DeactivateSpaceAsync(lot.Id, space.Id, ct)));

        Assert.Equal(reservation.Id, Assert.Single(ex.ReservationIds));
        Assert.True(await IsActiveAsync(lot.Id, space.Id, ct));
    }

    [Fact]
    public async Task DeactivateSpace_WhenTheOnlyReservationHasEnded_Deactivates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddLotWithSpaceAsync(ct);
        await BookTenToElevenAsync(space.Id, ct);

        // The end is exclusive: at 11:00 the reservation is finished.
        await RunAsync(Local(11), s => s.DeactivateSpaceAsync(lot.Id, space.Id, ct));

        Assert.False(await IsActiveAsync(lot.Id, space.Id, ct));
    }

    [Fact]
    public async Task DeactivateSpace_WhenTheReservationIsCancelled_Deactivates()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await AddLotWithSpaceAsync(ct);
        var reservation = await BookTenToElevenAsync(space.Id, ct);
        await using (var db = fixture.CreateDbContext())
        {
            await new CancelReservationService(db, new FakeTimeProvider(Local(8, 30)))
                .CancelAsync(reservation.Id, new Actor(fixture.UserId, CancellationActor.Customer), ct);
        }

        await RunAsync(Local(9), s => s.DeactivateSpaceAsync(lot.Id, space.Id, ct));

        Assert.False(await IsActiveAsync(lot.Id, space.Id, ct));
    }

    [Fact]
    public async Task DeactivateSpace_SpaceOfAnotherLot_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, _) = await AddLotWithSpaceAsync(ct);
        var (_, foreignSpace) = await AddLotWithSpaceAsync(ct);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            RunAsync(Local(9), s => s.DeactivateSpaceAsync(lot.Id, foreignSpace.Id, ct)));
    }

    // A fresh context per call, like one request per context in the API.
    private async Task<T> RunAsync<T>(DateTimeOffset now, Func<AdminLotService, Task<T>> action)
    {
        await using var db = fixture.CreateDbContext();
        return await action(new AdminLotService(db, new PostgresDatabaseErrors(), new FakeTimeProvider(now)));
    }

    private async Task RunAsync(DateTimeOffset now, Func<AdminLotService, Task> action)
    {
        await using var db = fixture.CreateDbContext();
        await action(new AdminLotService(db, new PostgresDatabaseErrors(), new FakeTimeProvider(now)));
    }

    private async Task<(ParkingLot Lot, ParkingSpace Space)> AddLotWithSpaceAsync(CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return (lot, space);
    }

    private async Task<Reservation> BookTenToElevenAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CreateReservationService(db, new PostgresDatabaseErrors(), new FakeTimeProvider(Local(8)), Sarajevo);
        return await service.CreateAsync(fixture.UserId, spaceId, Local(10), Local(11), TestPlates.Next(), ct);
    }

    private async Task<ParkingLot> LotAsync(Guid lotId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await db.ParkingLots.AsNoTracking().Include(l => l.Spaces).SingleAsync(l => l.Id == lotId, ct);
    }

    private async Task<bool> IsActiveAsync(Guid lotId, Guid spaceId, CancellationToken ct) =>
        (await LotAsync(lotId, ct)).Spaces.Single(s => s.Id == spaceId).IsActive;
}

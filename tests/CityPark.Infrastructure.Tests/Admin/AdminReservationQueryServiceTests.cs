using CityPark.Application.Admin;
using CityPark.Application.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Admin;

// The admin list spans all users, so every test filters by its own lot to ignore rows of other tests.
public sealed class AdminReservationQueryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static readonly DateTimeOffset BookedAt = Local(1, 8);

    // December 2026 is winter time in Sarajevo (UTC+1).
    private static DateTimeOffset Local(int day, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, day, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    private static DateOnly December(int day) => new(2026, 12, day);

    [Fact]
    public async Task List_ByLot_ReturnsTheReservationsOfEveryUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await CreateLotAsync(ct);
        var (otherLot, otherSpace) = await CreateLotAsync(ct);
        var userA = await CreateUserAsync(ct);
        var userB = await CreateUserAsync(ct);
        var a = Book(lot, space, userA, Local(2, 10), Local(2, 11));
        var b = Book(lot, space, userB, Local(3, 10), Local(3, 11));
        await SaveAsync([a, b, Book(otherLot, otherSpace, userA, Local(2, 10), Local(2, 11))], ct);

        var result = await ListAsync(new AdminReservationFilter(lotId: lot.Id), ct);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { b.Id, a.Id }, result.Items.Select(i => i.Id));
        Assert.Equal(new[] { userB, userA }, result.Items.Select(i => i.UserId));
        Assert.All(result.Items, i => Assert.Equal(lot.Id, i.LotId));
    }

    [Fact]
    public async Task List_StatusCancelled_ReturnsOnlyCancelledReservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await CreateLotAsync(ct);
        var user = await CreateUserAsync(ct);
        var cancelled = Book(lot, space, user, Local(2, 10), Local(2, 11), cancel: true);
        await SaveAsync([cancelled, Book(lot, space, user, Local(3, 10), Local(3, 11))], ct);

        var result = await ListAsync(new AdminReservationFilter(lotId: lot.Id, status: ReservationStatus.Cancelled), ct);

        var item = Assert.Single(result.Items);
        Assert.Equal(cancelled.Id, item.Id);
        Assert.Equal(ReservationStatus.Cancelled, item.Status);
        Assert.Equal(ReservationState.Cancelled, item.State);
    }

    [Fact]
    public async Task List_DateRange_MatchesTheLocalStartDay_BothEndsInclusive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await CreateLotAsync(ct);
        var user = await CreateUserAsync(ct);

        // 00:00 local on 3 December is 23:00 UTC on 2 December: the local day must decide.
        var midnightOn3 = Book(lot, space, user, Local(3, 0), Local(3, 1));
        var lateOn4 = Book(lot, space, user, Local(4, 22), Local(4, 23));
        await SaveAsync(
            [Book(lot, space, user, Local(2, 22), Local(2, 23)), midnightOn3, lateOn4, Book(lot, space, user, Local(5, 0), Local(5, 1))],
            ct);

        var range = await ListAsync(new AdminReservationFilter(lotId: lot.Id, from: December(3), to: December(4)), ct);
        var onlyThe2nd = await ListAsync(new AdminReservationFilter(lotId: lot.Id, from: December(2), to: December(2)), ct);

        Assert.Equal(new[] { lateOn4.Id, midnightOn3.Id }, range.Items.Select(i => i.Id));
        Assert.DoesNotContain(onlyThe2nd.Items, i => i.Id == midnightOn3.Id);
        Assert.Single(onlyThe2nd.Items);
    }

    [Fact]
    public async Task Get_AnyUsersReservation_ReturnsIt_AndAnUnknownIdIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, space) = await CreateLotAsync(ct);
        var owner = await CreateUserAsync(ct);
        var reservation = Book(lot, space, owner, Local(2, 10), Local(2, 11));
        await SaveAsync([reservation], ct);

        await using var db = fixture.CreateDbContext();
        var service = new AdminReservationQueryService(db, Sarajevo, new FakeTimeProvider(BookedAt));

        var item = await service.GetAsync(reservation.Id, ct);

        Assert.Equal(owner, item.UserId);
        Assert.Equal(space.Id, item.SpaceId);
        Assert.Equal(ReservationState.Upcoming, item.State);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Guid.CreateVersion7(), ct));
    }

    private static Reservation Book(
        ParkingLot lot, ParkingSpace space, Guid userId, DateTimeOffset start, DateTimeOffset end, bool cancel = false)
    {
        var clock = new FakeTimeProvider(BookedAt);
        var reservation = Reservation.Create(lot, space, new TimeInterval(start, end), new LicensePlate(TestPlates.Next()), userId, Sarajevo, clock);

        if (cancel)
        {
            reservation.Cancel(CancellationActor.Customer, clock);
        }

        return reservation;
    }

    private async Task<PagedResult<AdminReservation>> ListAsync(AdminReservationFilter filter, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new AdminReservationQueryService(db, Sarajevo, new FakeTimeProvider(BookedAt));
        return await service.ListAsync(filter, new PageRequest(1, 50), ct);
    }

    private async Task SaveAsync(Reservation[] reservations, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        db.Reservations.AddRange(reservations);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Guid> CreateUserAsync(CancellationToken ct)
    {
        var id = Guid.CreateVersion7();

        await using var db = fixture.CreateDbContext();
        db.Users.Add(new IdentityUser<Guid> { Id = id, UserName = $"user-{id}" });
        await db.SaveChangesAsync(ct);

        return id;
    }

    // Open around the clock, so a reservation can start at local midnight.
    private async Task<(ParkingLot Lot, ParkingSpace Space)> CreateLotAsync(CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            $"Lot {Guid.CreateVersion7()}", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(0, 0), new TimeOnly(23, 45)));
        var space = lot.AddSpace("A-1");

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);

        return (lot, space);
    }
}

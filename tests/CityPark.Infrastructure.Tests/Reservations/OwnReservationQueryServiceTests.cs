using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Reservations;

// Each test creates its own users and lot, so tests sharing the database do not see each other's rows.
public sealed class OwnReservationQueryServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Every reservation is booked on Tuesday 1 December 2026 at 08:00 and lies later in December.
    private static readonly DateTimeOffset BookedAt = Local(1, 8);

    private static int _plateNumber;

    // December 2026 is winter time in Sarajevo (UTC+1). Times in the tests are local.
    private static DateTimeOffset Local(int day, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, day, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    [Fact]
    public async Task List_ReturnsOnlyTheCallersReservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var caller = await CreateUserAsync(ct);
        var other = await CreateUserAsync(ct);
        var nobody = await CreateUserAsync(ct);

        var mine = new[]
        {
            Book(lot, spaces[0], caller, Local(2, 10), Local(2, 11)),
            Book(lot, spaces[0], caller, Local(3, 10), Local(3, 11)),
        };
        var theirs = Book(lot, spaces[0], other, Local(4, 10), Local(4, 11));
        await SaveAsync([.. mine, theirs], ct);

        var result = await ListAsync(caller, 1, 20, BookedAt, ct);
        var empty = await ListAsync(nobody, 1, 20, BookedAt, ct);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(mine.Select(r => r.Id).Order(), result.Items.Select(i => i.Id).Order());
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task List_PageSize100_IsClampedTo50()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var caller = await CreateUserAsync(ct);

        // 13 one-hour slots a day (08:00–21:00) from 2 December on; the first 51 of them.
        var reservations = Enumerable.Range(0, 51)
            .Select(i => Book(lot, spaces[0], caller, Local(2 + (i / 13), 8 + (i % 13)), Local(2 + (i / 13), 9 + (i % 13))))
            .ToArray();
        await SaveAsync(reservations, ct);

        var first = await ListAsync(caller, 1, 100, BookedAt, ct);
        var second = await ListAsync(caller, 2, 100, BookedAt, ct);

        Assert.Equal(50, first.PageSize);
        Assert.Equal(50, first.Items.Count);
        Assert.Equal(51, first.TotalCount);
        Assert.Equal(2, first.TotalPages);

        // Page 2 starts after the 50th row, the clamped size, not after the 100th.
        Assert.Single(second.Items);
    }

    [Fact]
    public async Task List_DerivesTheStateFromStatusAndTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var caller = await CreateUserAsync(ct);

        var cancelledInThePast = Book(lot, spaces[0], caller, Local(2, 7), Local(2, 8), cancel: true);
        var finished = Book(lot, spaces[0], caller, Local(2, 8), Local(2, 9));
        var inProgress = Book(lot, spaces[0], caller, Local(2, 10), Local(2, 11));
        var upcoming = Book(lot, spaces[0], caller, Local(2, 12), Local(2, 13));
        var cancelledUpcoming = Book(lot, spaces[0], caller, Local(2, 14), Local(2, 15), cancel: true);
        await SaveAsync([cancelledInThePast, finished, inProgress, upcoming, cancelledUpcoming], ct);

        var at1030 = StatesById(await ListAsync(caller, 1, 20, Local(2, 10, 30), ct));
        var at1100 = StatesById(await ListAsync(caller, 1, 20, Local(2, 11), ct));

        Assert.Equal(ReservationState.Cancelled, at1030[cancelledInThePast.Id]); // not Finished
        Assert.Equal(ReservationState.Finished, at1030[finished.Id]);
        Assert.Equal(ReservationState.InProgress, at1030[inProgress.Id]);
        Assert.Equal(ReservationState.Upcoming, at1030[upcoming.Id]);
        Assert.Equal(ReservationState.Cancelled, at1030[cancelledUpcoming.Id]);

        // The end is exclusive: at 11:00 the 10:00–11:00 reservation is over.
        Assert.Equal(ReservationState.Finished, at1100[inProgress.Id]);
    }

    [Fact]
    public async Task List_OrdersByStartDescending_AndIsStableAcrossPages()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(2, ct);
        var caller = await CreateUserAsync(ct);

        // Saved in ascending order; two pairs share a start time on different spaces.
        await SaveAsync(
            [
                Book(lot, spaces[0], caller, Local(2, 10), Local(2, 11)),
                Book(lot, spaces[1], caller, Local(2, 10), Local(2, 11)),
                Book(lot, spaces[0], caller, Local(3, 10), Local(3, 11)),
                Book(lot, spaces[1], caller, Local(3, 10), Local(3, 11)),
                Book(lot, spaces[0], caller, Local(4, 9), Local(4, 10)),
            ],
            ct);

        var all = (await ListAsync(caller, 1, 50, BookedAt, ct)).Items.Select(i => i.Id).ToList();

        var paged = new List<OwnReservation>();
        for (var page = 1; page <= 3; page++)
        {
            paged.AddRange((await ListAsync(caller, page, 2, BookedAt, ct)).Items);
        }

        // Every row exactly once, newest start first, in the same order as one big page.
        Assert.Equal(5, paged.Select(i => i.Id).Distinct().Count());
        Assert.Equal(paged.Select(i => i.Start).OrderDescending(), paged.Select(i => i.Start));
        Assert.Equal(all, paged.Select(i => i.Id));
        Assert.Equal(Local(4, 9), paged[0].Start);
    }

    [Fact]
    public async Task List_ReturnsLotSpaceAndPriceDetails_AndTracksNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var caller = await CreateUserAsync(ct);
        var reservation = Book(lot, spaces[0], caller, Local(2, 10), Local(2, 11, 30));
        await SaveAsync([reservation], ct);

        await using var db = fixture.CreateDbContext();
        var result = await new OwnReservationQueryService(db, new FakeTimeProvider(BookedAt))
            .ListAsync(caller, new PageRequest(1, 20), ct);

        var item = Assert.Single(result.Items);
        Assert.Equal(lot.Name, item.LotName);
        Assert.Equal("A-1", item.SpaceLabel);
        Assert.Equal(Local(2, 10), item.Start);
        Assert.Equal(TimeSpan.Zero, item.Start.Offset);
        Assert.Equal(reservation.Plate.Value, item.Plate);
        Assert.Equal(4.00m, item.Price); // 1.5 h → 2 billed hours at 2.00 (R8)
        Assert.Equal("BAM", item.Currency);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_OwnReservation_ReturnsItWithTheDerivedState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var caller = await CreateUserAsync(ct);
        var reservation = Book(lot, spaces[0], caller, Local(2, 10), Local(2, 11));
        await SaveAsync([reservation], ct);

        var item = await GetAsync(reservation.Id, caller, Local(2, 10, 30), ct);

        Assert.Equal(reservation.Id, item.Id);
        Assert.Equal(lot.Name, item.LotName);
        Assert.Equal(2.00m, item.Price);
        Assert.Equal(ReservationState.InProgress, item.State);
    }

    [Fact]
    public async Task Get_AnotherUsersReservation_ThrowsTheSameNotFoundAsAnUnknownId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (lot, spaces) = await CreateLotAsync(1, ct);
        var owner = await CreateUserAsync(ct);
        var stranger = await CreateUserAsync(ct);
        var reservation = Book(lot, spaces[0], owner, Local(2, 10), Local(2, 11));
        await SaveAsync([reservation], ct);
        var unknownId = Guid.CreateVersion7();

        var foreign = await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(reservation.Id, stranger, BookedAt, ct));
        var unknown = await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(unknownId, owner, BookedAt, ct));

        Assert.Equal($"Reservation {reservation.Id} was not found.", foreign.Message);
        Assert.Equal($"Reservation {unknownId} was not found.", unknown.Message);
    }

    private static Dictionary<Guid, ReservationState> StatesById(PagedResult<OwnReservation> result) =>
        result.Items.ToDictionary(i => i.Id, i => i.State);

    // A new plate per reservation: the plate exclusion constraint (R7) spans all lots and users,
    // and tests in this class share one database and the same December days.
    private static LicensePlate NextPlate()
    {
        var n = Interlocked.Increment(ref _plateNumber);
        return new LicensePlate($"A{n / 1000:00}K{n % 1000:000}");
    }

    private static Reservation Book(
        ParkingLot lot, ParkingSpace space, Guid userId, DateTimeOffset start, DateTimeOffset end, bool cancel = false)
    {
        var clock = new FakeTimeProvider(BookedAt);
        var reservation = Reservation.Create(
            lot, space, new TimeInterval(start, end), NextPlate(), userId, Sarajevo, clock);

        if (cancel)
        {
            reservation.Cancel(CancellationActor.Customer, clock);
        }

        return reservation;
    }

    // A fresh context per call, like one request per context in the API.
    private async Task<PagedResult<OwnReservation>> ListAsync(
        Guid userId, int page, int pageSize, DateTimeOffset now, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new OwnReservationQueryService(db, new FakeTimeProvider(now));
        return await service.ListAsync(userId, new PageRequest(page, pageSize), ct);
    }

    private async Task<OwnReservation> GetAsync(Guid reservationId, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await new OwnReservationQueryService(db, new FakeTimeProvider(now)).GetAsync(reservationId, userId, ct);
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

    private async Task<(ParkingLot Lot, ParkingSpace[] Spaces)> CreateLotAsync(int spaceCount, CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            $"Lot {Guid.CreateVersion7()}", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var spaces = Enumerable.Range(1, spaceCount).Select(i => lot.AddSpace($"A-{i}")).ToArray();

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);

        return (lot, spaces);
    }
}

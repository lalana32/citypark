using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace CityPark.Infrastructure.Tests.Reservations;

// Each test creates its own lot, so tests sharing the fixture's database cannot see each other's data.
public sealed class CreateReservationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Plate = "A12K345";

    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Tuesday 1 December 2026 is winter time in Sarajevo (UTC+1). Times in the tests are local.
    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    // "Now" is 08:00 local on the same day: every test interval is in the future and within the horizon.
    private static FakeTimeProvider Clock => new(Local(8));

    [Fact]
    public async Task ValidRequest_SavesActiveReservationWithSnapshottedPrice()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");

        var created = await CreateAsync(space.Id, Local(10), Local(11, 30), ct);

        await using var db = fixture.CreateDbContext();
        var saved = await db.Reservations.SingleAsync(r => r.Id == created.Id, ct);
        Assert.Equal(ReservationStatus.Active, saved.Status);
        Assert.Equal(4.00m, saved.Price); // 90 minutes are two started hours at 2.00 BAM (R8).
        Assert.Equal("BAM", saved.Currency);
        Assert.Equal(new TimeInterval(Local(10), Local(11, 30)), saved.Interval);
        Assert.Equal(fixture.UserId, saved.UserId);
    }

    [Fact]
    public async Task OverlappingActiveReservation_OnSameSpace_ThrowsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");
        await CreateAsync(space.Id, Local(10), Local(11), ct);

        var ex = await Assert.ThrowsAsync<ReservationConflictException>(() =>
            CreateAsync(space.Id, Local(10, 30), Local(11, 30), ct));

        Assert.Equal(space.Id, ex.SpaceId);
        Assert.IsType<DbUpdateException>(ex.InnerException);
        Assert.Equal(1, await CountAsync(space.Id, ct));
    }

    [Fact]
    public async Task TouchingInterval_OnSameSpace_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");
        await CreateAsync(space.Id, Local(10), Local(11), ct);

        await CreateAsync(space.Id, Local(11), Local(12), ct);

        Assert.Equal(2, await CountAsync(space.Id, ct));
    }

    [Fact]
    public async Task SameInterval_OnAnotherSpace_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await CreateAsync(Space(lot, "A-1").Id, Local(10), Local(11), ct);

        await CreateAsync(Space(lot, "A-2").Id, Local(10), Local(11), ct);

        Assert.Equal(1, await CountAsync(Space(lot, "A-2").Id, ct));
    }

    [Fact]
    public async Task SimultaneousOverlappingRequests_OnSameSpace_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");

        // Two contexts, two connections: no pre-check could stop both, only the constraint can.
        var results = await Task.WhenAll(
            TryCreateAsync(space.Id, Local(10), Local(11), ct),
            TryCreateAsync(space.Id, Local(10, 30), Local(11, 30), ct));

        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r is ReservationConflictException);
        Assert.Equal(1, await CountAsync(space.Id, ct));
    }

    [Theory]
    [InlineData(21, 30, 22, 30)] // After closing time (R3).
    [InlineData(10, 10, 11, 10)] // Off the 15-minute grid (R1).
    [InlineData(10, 0, 10, 15)] // Shorter than 30 minutes (R2).
    public async Task DomainRuleBroken_ThrowsValidationErrorAndWritesNothing(int startHour, int startMinute, int endHour, int endMinute)
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");

        // ThrowsAsync checks the exact type, so a DomainConflictException would fail this test.
        await Assert.ThrowsAsync<DomainException>(() =>
            CreateAsync(space.Id, Local(startHour, startMinute), Local(endHour, endMinute), ct));

        Assert.Equal(0, await CountAsync(space.Id, ct));
    }

    [Fact]
    public async Task InvalidPlate_ThrowsValidationErrorAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateAsync(space.Id, Local(10), Local(11), ct, plate: "NOT-A-PLATE"));

        Assert.Equal(0, await CountAsync(space.Id, ct));
    }

    [Fact]
    public async Task InactiveSpace_ThrowsValidationErrorAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct, inactiveLabel: "A-3");
        var space = Space(lot, "A-3");

        // R11: an inactive space receives no new reservations.
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync(space.Id, Local(10), Local(11), ct));

        Assert.Equal(0, await CountAsync(space.Id, ct));
    }

    [Fact]
    public async Task OtherDatabaseError_IsRethrownNotConvertedToConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        var space = Space(lot, "A-1");

        // No Identity user has this id: the insert fails with 23503 (foreign_key_violation), not 23P01.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateAsync(space.Id, Local(10), Local(11), ct, userId: Guid.CreateVersion7()));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task UnknownSpace_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<NotFoundException>(() => CreateAsync(Guid.CreateVersion7(), Local(10), Local(11), ct));
    }

    [Fact]
    public async Task InLot_PicksFirstFreeSpaceByLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await CreateAsync(Space(lot, "A-1").Id, Local(10), Local(11), ct);

        var created = await CreateInLotAsync(lot.Id, Local(10), Local(11, 30), ct);

        Assert.Equal(Space(lot, "A-2").Id, created.SpaceId);
        Assert.Equal(4.00m, created.Price);
        Assert.Equal(2, await CountInLotAsync(lot, ct));
    }

    [Fact]
    public async Task InLot_TouchingInterval_TakesFirstSpace()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await CreateAsync(Space(lot, "A-1").Id, Local(10), Local(11), ct);

        var created = await CreateInLotAsync(lot.Id, Local(11), Local(12), ct);

        Assert.Equal(Space(lot, "A-1").Id, created.SpaceId);
    }

    [Fact]
    public async Task InLot_SkipsInactiveSpace()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct, inactiveLabel: "A-1");

        var created = await CreateInLotAsync(lot.Id, Local(10), Local(11), ct);

        Assert.Equal(Space(lot, "A-2").Id, created.SpaceId);
    }

    [Fact]
    public async Task InLot_FullyBooked_ThrowsNoFreeSpaceAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);

        foreach (var space in lot.Spaces)
        {
            await CreateAsync(space.Id, Local(10), Local(11), ct);
        }

        var ex = await Assert.ThrowsAsync<NoFreeSpaceException>(() => CreateInLotAsync(lot.Id, Local(10, 30), Local(11, 30), ct));

        Assert.Equal(lot.Id, ex.LotId);
        Assert.Equal(3, await CountInLotAsync(lot, ct));
    }

    [Fact]
    public async Task InLot_DomainRuleBroken_ThrowsValidationErrorAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);

        // Off the 15-minute grid (R1).
        await Assert.ThrowsAsync<DomainException>(() => CreateInLotAsync(lot.Id, Local(10, 10), Local(11, 10), ct));

        Assert.Equal(0, await CountInLotAsync(lot, ct));
    }

    [Fact]
    public async Task InLot_UnknownLot_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<NotFoundException>(() => CreateInLotAsync(Guid.CreateVersion7(), Local(10), Local(11), ct));
    }

    private async Task<Reservation> CreateInLotAsync(Guid lotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CreateReservationService(db, new PostgresDatabaseErrors(), Clock, Sarajevo);
        return await service.CreateInLotAsync(fixture.UserId, lotId, start, end, Plate, ct);
    }

    private async Task<int> CountInLotAsync(ParkingLot lot, CancellationToken ct)
    {
        var spaceIds = lot.Spaces.Select(s => s.Id).ToList();
        await using var db = fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => spaceIds.Contains(r.SpaceId), ct);
    }

    // A fresh context per call, like one request per context in the API.
    private async Task<Reservation> CreateAsync(
        Guid spaceId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct,
        string plate = Plate,
        Guid? userId = null)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CreateReservationService(db, new PostgresDatabaseErrors(), Clock, Sarajevo);
        return await service.CreateAsync(userId ?? fixture.UserId, spaceId, start, end, plate, ct);
    }

    private async Task<Exception?> TryCreateAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        try
        {
            await CreateAsync(spaceId, start, end, ct);
            return null;
        }
        catch (ReservationConflictException ex)
        {
            return ex;
        }
    }

    private async Task<int> CountAsync(Guid spaceId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Reservations.CountAsync(r => r.SpaceId == spaceId, ct);
    }

    private static ParkingSpace Space(ParkingLot lot, string label) => lot.Spaces.Single(s => s.Label == label);

    private async Task<ParkingLot> AddLotAsync(CancellationToken ct, string? inactiveLabel = null)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));

        foreach (var label in new[] { "A-1", "A-2", "A-3" })
        {
            var space = lot.AddSpace(label);

            if (label == inactiveLabel)
            {
                space.Deactivate();
            }
        }

        await using var db = fixture.CreateDbContext();
        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return lot;
    }
}

using CityPark.Application.Availability;
using CityPark.Application.Common;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Availability;

// Each test creates its own lot, so tests sharing the fixture's database cannot see each other's data.
public sealed class AvailabilityServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Tuesday 1 December 2026 is winter time in Sarajevo (UTC+1). Times in the tests are local.
    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    // "Now" is 08:00 local on the same day: every test interval is in the future and within the horizon.
    private static FakeTimeProvider Clock => new(Local(8));

    [Fact]
    public async Task ActiveReservation_OverlappingInterval_LeavesTwoFreeSpaces()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Active, ct);

        var result = await QueryAsync(lot.Id, Local(10, 30), Local(11, 30), ct);

        Assert.Equal(3, result.TotalSpaces);
        Assert.Equal(2, result.AvailableSpaces);
        Assert.Equal(["A-2", "A-3"], result.FreeSpaces.Select(s => s.Label));
    }

    [Fact]
    public async Task Spaces_ListEveryActiveSpaceInLabelOrderWithItsFreeFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct, inactiveLabel: "A-3");
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Active, ct);

        var result = await QueryAsync(lot.Id, Local(10, 30), Local(11, 30), ct);

        Assert.Equal(
            [("A-1", false), ("A-2", true)],
            result.Spaces.Select(s => (s.Label, s.IsFree)));
        Assert.Equal(
            result.Spaces.Where(s => s.IsFree).Select(s => s.Id),
            result.FreeSpaces.Select(s => s.Id));
    }

    [Fact]
    public async Task IntervalWithLocalOffset_IsTheSameInstantAsUtc()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Active, ct);

        var winter = TimeSpan.FromHours(1);
        var result = await QueryAsync(
            lot.Id, new DateTimeOffset(2026, 12, 1, 10, 30, 0, winter), new DateTimeOffset(2026, 12, 1, 11, 30, 0, winter), ct);

        Assert.Equal(["A-2", "A-3"], result.FreeSpaces.Select(s => s.Label));
    }

    [Fact]
    public async Task ActiveReservation_TouchingInterval_LeavesAllSpacesFree()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Active, ct);

        // [10:00, 11:00) and [11:00, 12:00) touch but do not overlap: the end is exclusive.
        var result = await QueryAsync(lot.Id, Local(11), Local(12), ct);

        Assert.Equal(3, result.TotalSpaces);
        Assert.Equal(3, result.AvailableSpaces);
        Assert.Equal(["A-1", "A-2", "A-3"], result.FreeSpaces.Select(s => s.Label));
    }

    [Fact]
    public async Task CancelledReservation_DoesNotReduceAvailability()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Cancelled, ct);

        var result = await QueryAsync(lot.Id, Local(10, 30), Local(11, 30), ct);

        Assert.Equal(3, result.AvailableSpaces);
        Assert.Contains(result.FreeSpaces, s => s.Label == "A-1");
    }

    [Fact]
    public async Task InactiveSpace_IsNeverReturnedNorCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct, inactiveLabel: "A-3");

        var result = await QueryAsync(lot.Id, Local(10), Local(11), ct);

        // Capacity counts active spaces only (product brief, glossary).
        Assert.Equal(2, result.TotalSpaces);
        Assert.Equal(["A-1", "A-2"], result.FreeSpaces.Select(s => s.Label));
    }

    [Fact]
    public async Task UnknownLot_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<NotFoundException>(() => QueryAsync(Guid.CreateVersion7(), Local(10), Local(11), ct));
    }

    [Fact]
    public async Task IntervalOutsideOpeningHours_ThrowsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);

        // The lot closes at 22:00 (R3).
        await Assert.ThrowsAsync<DomainException>(() => QueryAsync(lot.Id, Local(21, 30), Local(22, 30), ct));
    }

    [Fact]
    public async Task IntervalOffTheGrid_ThrowsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);

        // 10:10 is not on the 15-minute grid (R1).
        await Assert.ThrowsAsync<DomainException>(() => QueryAsync(lot.Id, Local(10, 10), Local(11, 10), ct));
    }

    [Fact]
    public async Task IntervalBeyondBookingHorizon_ThrowsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);

        // Now is 1 December 08:00; 1 January 10:00 is more than 30 days ahead (R4).
        var start = Local(10).AddDays(31);

        await Assert.ThrowsAsync<DomainException>(() => QueryAsync(lot.Id, start, start.AddHours(1), ct));
    }

    [Fact]
    public async Task Availability_RunsAsOneSqlStatement_AfterTheLotLookup()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await AddLotAsync(ct);
        await AddReservationAsync(lot, "A-1", Local(10), Local(11), ReservationStatus.Active, ct);
        var commands = new List<string>();

        await using (var db = fixture.CreateLoggingDbContext(commands.Add))
        {
            await new AvailabilityService(db, Clock, Sarajevo).GetAvailabilityAsync(lot.Id, Local(10, 30), Local(11, 30), ct);
        }

        // 1: the lot by id, for its opening hours (R3). 2: active spaces with an EXISTS subquery on
        // reservations. More commands would mean a query per space (N+1).
        Assert.Equal(2, commands.Count);
        Assert.Contains("EXISTS", commands[1], StringComparison.Ordinal);
        Assert.Contains("reservations", commands[1], StringComparison.Ordinal);

        TestContext.Current.TestOutputHelper?.WriteLine(commands[1]);
    }

    private async Task<AvailabilityResult> QueryAsync(Guid lotId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await new AvailabilityService(db, Clock, Sarajevo).GetAvailabilityAsync(lotId, start, end, ct);
    }

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
        db.ParkingLots.Add(lot); // Saving the lot also inserts its spaces.
        await db.SaveChangesAsync(ct);
        return lot;
    }

    private async Task AddReservationAsync(
        ParkingLot lot,
        string label,
        DateTimeOffset start,
        DateTimeOffset end,
        ReservationStatus status,
        CancellationToken ct)
    {
        var space = lot.Spaces.Single(s => s.Label == label);
        var reservation = Reservation.Create(
            lot, space, new TimeInterval(start, end), new LicensePlate(TestPlates.Next()), fixture.UserId, Sarajevo, Clock);

        if (status == ReservationStatus.Cancelled)
        {
            reservation.Cancel(CancellationActor.Administrator, Clock);
        }

        await using var db = fixture.CreateDbContext();
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
    }
}

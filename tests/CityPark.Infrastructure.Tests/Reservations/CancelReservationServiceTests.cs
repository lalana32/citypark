using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;
using CityPark.Infrastructure.Persistence;
using CityPark.Infrastructure.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Infrastructure.Tests.Reservations;

// Each test creates its own lot and a 10:00–11:00 reservation on it, booked at 08:00.
public sealed class CancelReservationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Tuesday 1 December 2026, winter time in Sarajevo (UTC+1). Times in the tests are local.
    private static DateTimeOffset Local(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    private Actor Owner => new(fixture.UserId, CancellationActor.Customer);

    [Fact]
    public async Task Owner_TwentyMinutesBeforeStart_CancelsReservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);

        await CancelAsync(reservation.Id, Owner, Local(9, 40), ct);

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id, ct));
    }

    [Theory]
    [InlineData(10, 0)] // exactly at the start
    [InlineData(10, 10)] // in progress
    public async Task Owner_AtOrAfterStart_IsRejectedAndStaysActive(int hour, int minute)
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);

        // R9 keeps "until it starts"; the card's 15-minute cutoff was not adopted.
        await Assert.ThrowsAsync<DomainException>(() => CancelAsync(reservation.Id, Owner, Local(hour, minute), ct));

        Assert.Equal(ReservationStatus.Active, await StatusAsync(reservation.Id, ct));
    }

    [Fact]
    public async Task AnotherCustomer_GetsNotFoundAndReservationStaysActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);
        var stranger = new Actor(Guid.CreateVersion7(), CancellationActor.Customer);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CancelAsync(reservation.Id, stranger, Local(9, 40), ct));

        Assert.Equal($"Reservation {reservation.Id} was not found.", ex.Message);
        Assert.Equal(ReservationStatus.Active, await StatusAsync(reservation.Id, ct));
    }

    [Fact]
    public async Task UnknownReservation_ThrowsNotFoundWithTheSameMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var unknownId = Guid.CreateVersion7();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CancelAsync(unknownId, Owner, Local(9, 40), ct));

        Assert.Equal($"Reservation {unknownId} was not found.", ex.Message);
    }

    [Theory]
    [InlineData(9, 59)] // one minute before the start
    [InlineData(10, 30)] // in progress
    public async Task Administrator_CancelsAnyReservationAtAnyTime(int hour, int minute)
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);
        var admin = new Actor(Guid.CreateVersion7(), CancellationActor.Administrator);

        await CancelAsync(reservation.Id, admin, Local(hour, minute), ct);

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id, ct));
    }

    [Fact]
    public async Task CancellingTwice_SecondCallThrowsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);
        await CancelAsync(reservation.Id, Owner, Local(9, 40), ct);

        await Assert.ThrowsAsync<DomainConflictException>(() => CancelAsync(reservation.Id, Owner, Local(9, 41), ct));

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id, ct));
    }

    [Fact]
    public async Task AfterCancellation_OverlappingReservationOnSameSpace_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var reservation = await BookTenToElevenAsync(ct);
        await CancelAsync(reservation.Id, Owner, Local(9, 40), ct);

        // The exclusion constraint counts only Active rows (ADR-002), so the space is free again.
        var replacement = await CreateAsync(reservation.SpaceId, Local(10, 30), Local(11, 30), ct);

        Assert.Equal(ReservationStatus.Active, await StatusAsync(replacement.Id, ct));
    }

    // A fresh context per call, like one request per context in the API.
    private async Task CancelAsync(Guid reservationId, Actor actor, DateTimeOffset now, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CancelReservationService(db, new FakeTimeProvider(now));
        await service.CancelAsync(reservationId, actor, ct);
    }

    private async Task<Reservation> CreateAsync(Guid spaceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        var service = new CreateReservationService(db, new PostgresDatabaseErrors(), new FakeTimeProvider(Local(8)), Sarajevo);
        return await service.CreateAsync(fixture.UserId, spaceId, start, end, "A12K345", ct);
    }

    private async Task<Reservation> BookTenToElevenAsync(CancellationToken ct)
    {
        var lot = ParkingLot.Create(
            "Test lot", "Test street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");

        await using (var db = fixture.CreateDbContext())
        {
            db.ParkingLots.Add(lot);
            await db.SaveChangesAsync(ct);
        }

        return await CreateAsync(space.Id, Local(10), Local(11), ct);
    }

    private async Task<ReservationStatus> StatusAsync(Guid reservationId, CancellationToken ct)
    {
        await using var db = fixture.CreateDbContext();
        return await db.Reservations.Where(r => r.Id == reservationId).Select(r => r.Status).SingleAsync(ct);
    }
}

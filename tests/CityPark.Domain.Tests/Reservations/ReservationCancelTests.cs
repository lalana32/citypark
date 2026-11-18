using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.Extensions.Time.Testing;

namespace CityPark.Domain.Tests.Reservations;

public class ReservationCancelTests
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Winter time in Sarajevo (UTC+1), Wednesday 18 November 2026.
    private static DateTimeOffset Today(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 18, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    private static FakeTimeProvider ClockAt(DateTimeOffset now) => new(now);

    // Created at 09:00 for 10:00–11:00; each test then sets the clock to the moment of the cancel.
    private static Reservation TenToEleven()
    {
        var lot = ParkingLot.Create(
            "Centar", "Ferhadija 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");

        return Reservation.Create(
            lot, space, new TimeInterval(Today(10), Today(11)), new LicensePlate("A12K345"), Sarajevo, ClockAt(Today(9)));
    }

    [Fact]
    public void Cancel_ByCustomerOneMinuteBeforeStart_CancelsReservation()
    {
        var reservation = TenToEleven();

        reservation.Cancel(CancellationActor.Customer, ClockAt(Today(9, 59)));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Theory]
    [InlineData(0)] // exactly at the start
    [InlineData(1)]
    public void Cancel_ByCustomerAtOrAfterStart_ThrowsDomainException(int minutesAfterStart)
    {
        var reservation = TenToEleven();

        Assert.Throws<DomainException>(() =>
            reservation.Cancel(CancellationActor.Customer, ClockAt(Today(10, minutesAfterStart))));
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Theory]
    [InlineData(9, 59)] // before the start
    [InlineData(10, 30)] // in progress
    [InlineData(12, 0)] // finished
    public void Cancel_ByAdministrator_IsAllowedAtAnyTime(int hour, int minute)
    {
        var reservation = TenToEleven();

        reservation.Cancel(CancellationActor.Administrator, ClockAt(Today(hour, minute)));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Theory]
    [InlineData(CancellationActor.Customer)]
    [InlineData(CancellationActor.Administrator)]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainConflictException(CancellationActor secondActor)
    {
        var reservation = TenToEleven();
        reservation.Cancel(CancellationActor.Customer, ClockAt(Today(9, 30)));

        Assert.Throws<DomainConflictException>(() => reservation.Cancel(secondActor, ClockAt(Today(9, 31))));
    }

    [Fact]
    public void Cancel_WithUnknownActor_ThrowsArgumentOutOfRangeException()
    {
        var reservation = TenToEleven();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reservation.Cancel((CancellationActor)99, ClockAt(Today(9, 30))));
    }

    [Fact]
    public void Cancel_WhenClockIsNull_ThrowsArgumentNullException()
    {
        var reservation = TenToEleven();

        Assert.Throws<ArgumentNullException>(() => reservation.Cancel(CancellationActor.Administrator, null!));
    }
}

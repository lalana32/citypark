using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.Extensions.Time.Testing;

namespace CityPark.Domain.Tests.Reservations;

// The expression is compiled and run in memory here; the availability integration tests run the
// same expression as SQL against PostgreSQL.
public class ReservationActiveAndOverlappingTests
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    // Wednesday 18 November 2026, winter time (UTC+1).
    private static DateTimeOffset Today(int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 18, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    private static FakeTimeProvider Clock => new(Today(9));

    // Active 10:00–11:00 on its own space.
    private static Reservation TenToEleven()
    {
        var lot = ParkingLot.Create(
            "Centar", "Ferhadija 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var space = lot.AddSpace("A-1");
        return Reservation.Create(
            lot, space, new TimeInterval(Today(10), Today(11)), new LicensePlate("A12K345"), Guid.CreateVersion7(), Sarajevo, Clock);
    }

    private static bool Blocks(Reservation reservation, DateTimeOffset start, DateTimeOffset end) =>
        Reservation.ActiveAndOverlapping(new TimeInterval(start, end)).Compile()(reservation);

    [Theory]
    [InlineData(10, 30, 11, 30)] // overlaps the end
    [InlineData(9, 30, 10, 30)] // overlaps the start
    [InlineData(10, 15, 10, 45)] // inside
    [InlineData(9, 0, 12, 0)] // around
    public void Active_OverlappingInterval_Blocks(int startHour, int startMinute, int endHour, int endMinute)
    {
        Assert.True(Blocks(TenToEleven(), Today(startHour, startMinute), Today(endHour, endMinute)));
    }

    [Theory]
    [InlineData(11, 0, 12, 0)] // starts when the reservation ends
    [InlineData(9, 0, 10, 0)] // ends when the reservation starts
    public void Active_TouchingInterval_DoesNotBlock(int startHour, int startMinute, int endHour, int endMinute)
    {
        Assert.False(Blocks(TenToEleven(), Today(startHour, startMinute), Today(endHour, endMinute)));
    }

    [Fact]
    public void Cancelled_OverlappingInterval_DoesNotBlock()
    {
        var reservation = TenToEleven();
        reservation.Cancel(CancellationActor.Administrator, Clock);

        Assert.False(Blocks(reservation, Today(10, 30), Today(11, 30)));
    }
}

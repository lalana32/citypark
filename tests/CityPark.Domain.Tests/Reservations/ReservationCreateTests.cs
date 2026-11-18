using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.Extensions.Time.Testing;

namespace CityPark.Domain.Tests.Reservations;

public class ReservationCreateTests
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static readonly LicensePlate Plate = new("A12K345");

    private static readonly Guid UserId = Guid.CreateVersion7();

    private readonly ParkingLot _lot;

    private readonly ParkingSpace _space;

    public ReservationCreateTests()
    {
        _lot = ParkingLot.Create(
            "Centar", "Ferhadija 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        _space = _lot.AddSpace("A-1");
    }

    // November and December 2026 are winter time in Sarajevo (UTC+1); these tests avoid DST change days.
    private static DateTimeOffset Local(int month, int day, int hour, int minute) =>
        new DateTimeOffset(2026, month, day, hour, minute, 0, TimeSpan.FromHours(1)).ToUniversalTime();

    // "Today" is Wednesday 18 November 2026.
    private static DateTimeOffset Today(int hour, int minute = 0) => Local(11, 18, hour, minute);

    private static FakeTimeProvider ClockAt(DateTimeOffset now) => new(now);

    // A new clock on every read, so a test that advances its clock cannot affect another test.
    private static FakeTimeProvider NineOClock => ClockAt(Today(9));

    private Reservation Create(DateTimeOffset start, DateTimeOffset end, TimeProvider? clock = null) =>
        Reservation.Create(_lot, _space, new TimeInterval(start, end), Plate, UserId, Sarajevo, clock ?? NineOClock);

    [Fact]
    public void Create_WithValidRequest_ReturnsActiveReservationWithSnapshot()
    {
        var reservation = Create(Today(10), Today(11, 15));

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(_space.Id, reservation.SpaceId);
        Assert.Equal(UserId, reservation.UserId);
        Assert.Equal(new TimeInterval(Today(10), Today(11, 15)), reservation.Interval);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.Equal(Plate, reservation.Plate);
        Assert.Equal("BAM", reservation.Currency);
    }

    [Fact]
    public void Create_StoresTimesAsUtcDateTime()
    {
        var reservation = Create(Today(10), Today(11));

        // 10:00 local at UTC+1 is 09:00 UTC.
        Assert.Equal(new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc), reservation.StartTime);
        Assert.Equal(DateTimeKind.Utc, reservation.StartTime.Kind);
        Assert.Equal(DateTimeKind.Utc, reservation.EndTime.Kind);
    }

    // R8: 75 minutes at 2.00 BAM is billed as 2 hours.
    [Fact]
    public void Create_PricesEveryStartedHourFromTheLotRate()
    {
        var reservation = Create(Today(10), Today(11, 15));

        Assert.Equal(4.00m, reservation.Price);
    }

    // R1: time grid.
    [Fact]
    public void Create_WhenStartIsOffGrid_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Today(10, 10), Today(11, 15)));
    }

    [Fact]
    public void Create_WhenStartIsOnGrid_Succeeds()
    {
        var reservation = Create(Today(10, 15), Today(11, 15));

        Assert.Equal(Today(10, 15), reservation.Interval.Start);
    }

    [Fact]
    public void Create_WhenEndIsOffGrid_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Today(10), Today(11, 10)));
    }

    [Fact]
    public void Create_WhenStartHasSeconds_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Today(10).AddSeconds(30), Today(11)));
    }

    // R2: minimum duration.
    [Theory]
    [InlineData(15)]
    [InlineData(25)]
    public void Create_WhenShorterThanThirtyMinutes_ThrowsDomainException(int minutes)
    {
        Assert.Throws<DomainException>(() => Create(Today(10), Today(10).AddMinutes(minutes)));
    }

    [Fact]
    public void Create_WhenExactlyThirtyMinutes_Succeeds()
    {
        var reservation = Create(Today(10), Today(10, 30));

        Assert.Equal(TimeSpan.FromMinutes(30), reservation.Interval.Duration);
    }

    // R3: single day, inside opening hours (07:00–22:00). The full rule is tested in OpeningHoursTests.
    [Fact]
    public void Create_WhenEndingAtClosingTime_Succeeds()
    {
        var reservation = Create(Today(21), Today(22));

        Assert.Equal(Today(22), reservation.Interval.End);
    }

    [Fact]
    public void Create_WhenCrossingClosingTime_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Today(21, 30), Today(22, 30)));
    }

    [Fact]
    public void Create_WhenCrossingMidnight_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Today(23), Local(11, 19, 1, 0)));
    }

    // R4: booking horizon. Now is 18 November, 09:00.
    [Fact]
    public void Create_WhenStartIsExactlyThirtyDaysAhead_Succeeds()
    {
        var reservation = Create(Local(12, 18, 9, 0), Local(12, 18, 10, 0));

        Assert.Equal(Local(12, 18, 9, 0), reservation.Interval.Start);
    }

    [Fact]
    public void Create_WhenStartIsFifteenMinutesBeyondThirtyDays_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Local(12, 18, 9, 15), Local(12, 18, 10, 15)));
    }

    [Fact]
    public void Create_WhenStartIsThirtyOneDaysAhead_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Create(Local(12, 19, 9, 0), Local(12, 19, 10, 0)));
    }

    // R5: start tolerance. The start is 10:00 and the clock moves around it.
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Create_WhenStartIsAtMostFiveMinutesInThePast_Succeeds(int minutesLate)
    {
        var clock = ClockAt(Today(10, minutesLate));

        var reservation = Create(Today(10), Today(11), clock);

        Assert.Equal(Today(10), reservation.Interval.Start);
    }

    [Fact]
    public void Create_WhenStartIsSixMinutesInThePast_ThrowsDomainException()
    {
        var clock = ClockAt(Today(10, 6));

        Assert.Throws<DomainException>(() => Create(Today(10), Today(11), clock));
    }

    [Fact]
    public void Create_WhenSpaceBelongsToAnotherLot_ThrowsDomainException()
    {
        var otherLot = ParkingLot.Create(
            "Other", "Other street 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));
        var foreignSpace = otherLot.AddSpace("A-1");

        Assert.Throws<DomainException>(() => Reservation.Create(
            _lot, foreignSpace, new TimeInterval(Today(10), Today(11)), Plate, UserId, Sarajevo, NineOClock));
    }

    [Fact]
    public void Create_WhenUserIdIsEmpty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => Reservation.Create(
            _lot, _space, new TimeInterval(Today(10), Today(11)), Plate, Guid.Empty, Sarajevo, NineOClock));

        Assert.Equal("userId", ex.ParamName);
    }

    // Version 7 ids start with a timestamp, so new rows land at the end of the primary key index.
    [Fact]
    public void Create_AssignsVersion7Id()
    {
        Assert.Equal(7, Create(Today(10), Today(11)).Id.Version);
    }

    [Fact]
    public void Create_WhenClockIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Reservation.Create(
            _lot, _space, new TimeInterval(Today(10), Today(11)), Plate, UserId, Sarajevo, null!));
    }

    [Fact]
    public void IsFinished_BeforeEnd_IsFalse_AndAtEnd_IsTrue()
    {
        var reservation = Create(Today(10), Today(11));
        var clock = ClockAt(Today(10, 59));

        Assert.False(reservation.IsFinished(clock));

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(reservation.IsFinished(clock));
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }
}

using CityPark.Domain.Reservations;

namespace CityPark.Domain.Tests.Reservations;

public class ReservationStateTests
{
    // 10:00–11:00 UTC on Tuesday 1 December 2026.
    private static readonly DateTime Start = new(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime End = new(2026, 12, 1, 11, 0, 0, DateTimeKind.Utc);

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 12, 1, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(9, 59, ReservationState.Upcoming)]
    [InlineData(10, 0, ReservationState.InProgress)] // the start belongs to the interval
    [InlineData(10, 59, ReservationState.InProgress)]
    [InlineData(11, 0, ReservationState.Finished)] // the end does not
    [InlineData(15, 0, ReservationState.Finished)]
    public void StateAt_ActiveReservation_FollowsTheClock(int hour, int minute, ReservationState expected)
    {
        Assert.Equal(expected, Reservation.StateAt(ReservationStatus.Active, Start, End, At(hour, minute)));
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 30)]
    [InlineData(12, 0)] // also after the end: Cancelled, not Finished
    public void StateAt_CancelledReservation_IsCancelledAtAnyTime(int hour, int minute)
    {
        Assert.Equal(
            ReservationState.Cancelled,
            Reservation.StateAt(ReservationStatus.Cancelled, Start, End, At(hour, minute)));
    }

    [Fact]
    public void StateAt_NowWithLocalOffset_ComparesInstantsNotWallClock()
    {
        // 11:30 in Sarajevo (UTC+1) is 10:30 UTC.
        var now = new DateTimeOffset(2026, 12, 1, 11, 30, 0, TimeSpan.FromHours(1));

        Assert.Equal(ReservationState.InProgress, Reservation.StateAt(ReservationStatus.Active, Start, End, now));
    }
}

using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

namespace CityPark.Domain.Tests.Lots;

public class OpeningHoursTests
{
    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static readonly OpeningHours DayWindow = new(new TimeOnly(7, 0), new TimeOnly(22, 0));

    private static readonly OpeningHours AllDayWindow = new(new TimeOnly(0, 0), new TimeOnly(23, 59));

    // Local wall-clock time with an explicit offset (+1 winter, +2 summer), converted to UTC.
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int offsetHours) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(offsetHours)).ToUniversalTime();

    // 18 November 2026 is an ordinary winter day (UTC+1).
    private static DateTimeOffset Winter(int hour, int minute = 0) => Local(2026, 11, 18, hour, minute, 1);

    private static TimeInterval Between(DateTimeOffset start, DateTimeOffset end) => new(start, end);

    [Fact]
    public void Constructor_WithValidWindow_ExposesOpensAtAndClosesAt()
    {
        var hours = new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0));

        Assert.Equal(new TimeOnly(7, 0), hours.OpensAt);
        Assert.Equal(new TimeOnly(22, 0), hours.ClosesAt);
    }

    [Fact]
    public void Constructor_WhenCloseEqualsOpen_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new OpeningHours(new TimeOnly(7, 0), new TimeOnly(7, 0)));
    }

    [Fact]
    public void Constructor_WhenCloseIsBeforeOpen_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new OpeningHours(new TimeOnly(22, 0), new TimeOnly(7, 0)));
    }

    [Fact]
    public void Contains_WhenIntervalIsInsideWindow_ReturnsTrue()
    {
        Assert.True(DayWindow.Contains(Between(Winter(10), Winter(12)), Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalStartsAtOpeningTime_ReturnsTrue()
    {
        Assert.True(DayWindow.Contains(Between(Winter(7), Winter(8)), Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalEndsAtClosingTime_ReturnsTrue()
    {
        Assert.True(DayWindow.Contains(Between(Winter(21), Winter(22)), Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalStartsBeforeOpeningTime_ReturnsFalse()
    {
        Assert.False(DayWindow.Contains(Between(Winter(6, 45), Winter(8)), Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalCrossesClosingTime_ReturnsFalse()
    {
        Assert.False(DayWindow.Contains(Between(Winter(21, 30), Winter(22, 30)), Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalCrossesLocalMidnight_ReturnsFalse()
    {
        // Both ends are inside an all-day window, so only the same-local-day rule rejects it.
        var interval = Between(Winter(23), Local(2026, 11, 19, 1, 0, 1));

        Assert.False(AllDayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalEndsAtLocalMidnight_ReturnsFalse()
    {
        // 00:00 belongs to the next local day, so a window cannot be used up to midnight.
        var interval = Between(Winter(23), Local(2026, 11, 19, 0, 0, 1));

        Assert.False(AllDayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalCrossesLocalButNotUtcMidnight_ReturnsFalse()
    {
        // 22:30–23:30 UTC is 23:30–00:30 local: one UTC day, but two local days.
        var interval = Between(
            new DateTimeOffset(2026, 11, 18, 22, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 18, 23, 30, 0, TimeSpan.Zero));

        Assert.False(AllDayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_WhenIntervalCrossesUtcButNotLocalMidnight_ReturnsTrue()
    {
        // 23:30–00:30 UTC is 00:30–01:30 local: two UTC days, but one local day.
        var interval = Between(
            new DateTimeOffset(2026, 11, 17, 23, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 18, 0, 30, 0, TimeSpan.Zero));

        Assert.True(AllDayWindow.Contains(interval, Sarajevo));
    }

    // 28 March 2027: clocks jump from 02:00 (+1) to 03:00 (+2); the local day has 23 hours.
    [Fact]
    public void Contains_OnSpringForwardDay_UsesSummerOffsetForEveningInterval()
    {
        // 21:00–22:00 at +2 is 19:00–20:00 UTC. With the winter offset it would look like 20:00–21:00.
        var interval = Between(Local(2027, 3, 28, 21, 0, 2), Local(2027, 3, 28, 22, 0, 2));

        Assert.True(DayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_OnSpringForwardDay_RejectsIntervalCrossingClosingTime()
    {
        var interval = Between(Local(2027, 3, 28, 21, 30, 2), Local(2027, 3, 28, 22, 30, 2));

        Assert.False(DayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_OnSpringForwardDay_AcceptsIntervalAcrossTheGap()
    {
        // 01:30 (+1) to 03:30 (+2) shows two hours on the wall clock but lasts one real hour.
        var hours = new OpeningHours(new TimeOnly(1, 0), new TimeOnly(5, 0));
        var interval = Between(Local(2027, 3, 28, 1, 30, 1), Local(2027, 3, 28, 3, 30, 2));

        Assert.Equal(TimeSpan.FromHours(1), interval.Duration);
        Assert.True(hours.Contains(interval, Sarajevo));
    }

    // 25 October 2026: clocks fall back from 03:00 (+2) to 02:00 (+1); the local day has 25 hours.
    [Fact]
    public void Contains_OnFallBackDay_UsesWinterOffsetForEveningInterval()
    {
        // 21:00–22:00 at +1 is 20:00–21:00 UTC. With the summer offset it would look like 22:00–23:00.
        var interval = Between(Local(2026, 10, 25, 21, 0, 1), Local(2026, 10, 25, 22, 0, 1));

        Assert.True(DayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_OnFallBackDay_RejectsIntervalCrossingClosingTime()
    {
        var interval = Between(Local(2026, 10, 25, 21, 30, 1), Local(2026, 10, 25, 22, 30, 1));

        Assert.False(DayWindow.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_OnFallBackDay_AcceptsIntervalInsideTheRepeatedHour()
    {
        // 02:30 (+2) to 02:30 (+1) is the same wall-clock time twice, one real hour apart.
        var hours = new OpeningHours(new TimeOnly(1, 0), new TimeOnly(5, 0));
        var interval = Between(Local(2026, 10, 25, 2, 30, 2), Local(2026, 10, 25, 2, 30, 1));

        Assert.Equal(TimeSpan.FromHours(1), interval.Duration);
        Assert.True(hours.Contains(interval, Sarajevo));
    }

    [Fact]
    public void Contains_WhenTimeZoneIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DayWindow.Contains(Between(Winter(10), Winter(11)), null!));
    }
}

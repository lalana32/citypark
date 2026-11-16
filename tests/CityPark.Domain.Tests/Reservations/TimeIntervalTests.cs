using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Domain.Tests.Reservations;

public class TimeIntervalTests
{
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 11, 16, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenEndEqualsStart_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new TimeInterval(At(10), At(10)));
    }

    [Fact]
    public void Constructor_WhenEndIsBeforeStart_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new TimeInterval(At(11), At(10)));
    }

    [Fact]
    public void Constructor_WhenStartIsNotUtc_ThrowsDomainException()
    {
        var start = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<DomainException>(() => new TimeInterval(start, At(12)));
    }

    [Fact]
    public void Constructor_WhenEndIsNotUtc_ThrowsDomainException()
    {
        var end = new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<DomainException>(() => new TimeInterval(At(10), end));
    }

    [Fact]
    public void Constructor_WithValidUtcValues_ExposesStartAndEnd()
    {
        var interval = new TimeInterval(At(10), At(11));

        Assert.Equal(At(10), interval.Start);
        Assert.Equal(At(11), interval.End);
    }

    [Fact]
    public void Overlaps_WhenIntervalsOnlyTouch_ReturnsFalse()
    {
        var first = new TimeInterval(At(10), At(11));
        var second = new TimeInterval(At(11), At(12));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_WhenIntervalsPartiallyOverlap_ReturnsTrue()
    {
        var first = new TimeInterval(At(10), At(11));
        var second = new TimeInterval(At(10, 30), At(11, 30));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_WhenOneIntervalFullyContainsTheOther_ReturnsTrue()
    {
        var outer = new TimeInterval(At(10), At(12));
        var inner = new TimeInterval(At(10, 30), At(11));

        Assert.True(outer.Overlaps(inner));
    }

    [Fact]
    public void Overlaps_WhenIntervalsAreIdentical_ReturnsTrue()
    {
        var first = new TimeInterval(At(10), At(11));
        var second = new TimeInterval(At(10), At(11));

        Assert.True(first.Overlaps(second));
    }

    [Fact]
    public void Duration_ReturnsDifferenceBetweenEndAndStart()
    {
        var interval = new TimeInterval(At(10), At(11, 30));

        Assert.Equal(TimeSpan.FromMinutes(90), interval.Duration);
    }

    [Fact]
    public void Contains_WhenInstantIsInsideInterval_ReturnsTrue()
    {
        var interval = new TimeInterval(At(10), At(11));

        Assert.True(interval.Contains(At(10, 30)));
    }

    [Fact]
    public void Contains_WhenInstantIsOutsideInterval_ReturnsFalse()
    {
        var interval = new TimeInterval(At(10), At(11));

        Assert.False(interval.Contains(At(12)));
    }
}

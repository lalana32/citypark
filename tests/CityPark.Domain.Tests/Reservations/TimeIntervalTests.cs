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

    [Fact]
    public void Contains_AtStart_ReturnsTrue()
    {
        var interval = new TimeInterval(At(10), At(11));

        Assert.True(interval.Contains(At(10)));
    }

    [Fact]
    public void Contains_AtEnd_ReturnsFalse()
    {
        var interval = new TimeInterval(At(10), At(11));

        Assert.False(interval.Contains(At(11)));
    }

    [Fact]
    public void Constructor_WithOneTickInterval_IsValid()
    {
        var start = At(10);

        var interval = new TimeInterval(start, start.AddTicks(1));

        Assert.Equal(TimeSpan.FromTicks(1), interval.Duration);
    }

    [Fact]
    public void Overlaps_WhenIntervalsShareOnlyOneTick_ReturnsTrue()
    {
        var first = new TimeInterval(At(10), At(11).AddTicks(1));
        var second = new TimeInterval(At(11), At(12));

        Assert.True(first.Overlaps(second));
    }

    [Theory]
    [InlineData(10, 11, 11, 12, false)]
    [InlineData(10, 11, 12, 13, false)]
    [InlineData(10, 12, 11, 13, true)]
    [InlineData(10, 13, 11, 12, true)]
    public void Overlaps_IsSymmetric(int firstStart, int firstEnd, int secondStart, int secondEnd, bool expected)
    {
        var first = new TimeInterval(At(firstStart), At(firstEnd));
        var second = new TimeInterval(At(secondStart), At(secondEnd));

        Assert.Equal(expected, first.Overlaps(second));
        Assert.Equal(expected, second.Overlaps(first));
    }

    [Fact]
    public void Equality_WhenStartAndEndAreEqual_IntervalsAreEqual()
    {
        var first = new TimeInterval(At(10), At(11));
        var second = new TimeInterval(At(10), At(11));

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}

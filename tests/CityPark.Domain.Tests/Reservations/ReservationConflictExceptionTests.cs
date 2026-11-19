using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Domain.Tests.Reservations;

public class ReservationConflictExceptionTests
{
    private static readonly TimeInterval Interval = new(
        new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void IsADomainConflict_SoTheApiCanMapItTo409()
    {
        DomainConflictException exception = new ReservationConflictException(Guid.CreateVersion7(), Interval);

        Assert.IsType<ReservationConflictException>(exception);
    }

    [Fact]
    public void ExposesSpaceIntervalAndCause()
    {
        var spaceId = Guid.CreateVersion7();
        var cause = new InvalidOperationException("database error");

        var exception = new ReservationConflictException(spaceId, Interval, cause);

        Assert.Equal(spaceId, exception.SpaceId);
        Assert.Equal(Interval, exception.Interval);
        Assert.Same(cause, exception.InnerException);
        Assert.Contains(spaceId.ToString(), exception.Message, StringComparison.Ordinal);
    }
}

using CityPark.Application.Admin;
using CityPark.Application.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Application.Tests.Admin;

public class AdminReservationFilterTests
{
    [Fact]
    public void Constructor_SameDayRange_IsAccepted()
    {
        var day = new DateOnly(2026, 12, 1);

        var filter = new AdminReservationFilter(from: day, to: day, status: ReservationStatus.Cancelled);

        Assert.Equal(day, filter.From);
        Assert.Equal(day, filter.To);
        Assert.Equal(ReservationStatus.Cancelled, filter.Status);
    }

    [Fact]
    public void Constructor_FromAfterTo_ThrowsForTo()
    {
        var ex = Assert.Throws<RequestValidationException>(
            () => new AdminReservationFilter(from: new DateOnly(2026, 12, 2), to: new DateOnly(2026, 12, 1)));

        Assert.True(ex.Errors?.ContainsKey("to"));
    }

    [Fact]
    public void Constructor_UndefinedStatus_ThrowsForStatus()
    {
        var ex = Assert.Throws<RequestValidationException>(() => new AdminReservationFilter(status: (ReservationStatus)7));

        Assert.True(ex.Errors?.ContainsKey("status"));
    }
}

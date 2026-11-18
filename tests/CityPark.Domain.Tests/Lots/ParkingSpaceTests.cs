using CityPark.Domain.Lots;

namespace CityPark.Domain.Tests.Lots;

public class ParkingSpaceTests
{
    private static ParkingSpace NewSpace() =>
        ParkingLot.Create(
                "Centar",
                "Titova 1",
                new Rate(2.00m, "BAM"),
                new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)))
            .AddSpace("A-1");

    [Fact]
    public void NewSpace_IsActive()
    {
        Assert.True(NewSpace().IsActive);
    }

    [Fact]
    public void Deactivate_MakesSpaceInactive()
    {
        var space = NewSpace();

        space.Deactivate();

        Assert.False(space.IsActive);
    }

    [Fact]
    public void Activate_AfterDeactivate_MakesSpaceActiveAgain()
    {
        var space = NewSpace();
        space.Deactivate();

        space.Activate();

        Assert.True(space.IsActive);
    }
}

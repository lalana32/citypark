using CityPark.Domain.Lots;

namespace CityPark.Api.Admin;

public sealed record SpaceResponse(Guid Id, string Label, bool IsActive)
{
    public static SpaceResponse From(ParkingSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);

        return new(space.Id, space.Label, space.IsActive);
    }
}

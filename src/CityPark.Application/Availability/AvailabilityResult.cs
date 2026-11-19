namespace CityPark.Application.Availability;

/// <summary>
/// Free spaces of a lot for one interval. <see cref="TotalSpaces"/> is the capacity: active spaces only.
/// </summary>
public sealed record AvailabilityResult(int TotalSpaces, IReadOnlyList<FreeSpace> FreeSpaces)
{
    public int AvailableSpaces => FreeSpaces.Count;
}

public sealed record FreeSpace(Guid Id, string Label);

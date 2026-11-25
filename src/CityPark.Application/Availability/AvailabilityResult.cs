namespace CityPark.Application.Availability;

/// <summary>
/// Every active space of a lot for one interval, in label order, with whether it is free.
/// <see cref="TotalSpaces"/> is the capacity: active spaces only.
/// </summary>
public sealed record AvailabilityResult(IReadOnlyList<SpaceAvailability> Spaces)
{
    public int TotalSpaces => Spaces.Count;

    public IReadOnlyList<FreeSpace> FreeSpaces =>
        [.. Spaces.Where(s => s.IsFree).Select(s => new FreeSpace(s.Id, s.Label))];

    public int AvailableSpaces => Spaces.Count(s => s.IsFree);
}

public sealed record SpaceAvailability(Guid Id, string Label, bool IsFree);

public sealed record FreeSpace(Guid Id, string Label);

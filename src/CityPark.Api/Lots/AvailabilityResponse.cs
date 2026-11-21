using CityPark.Application.Availability;

namespace CityPark.Api.Lots;

/// <summary>
/// Free spaces of a lot for one interval. <c>start</c> and <c>end</c> are echoed with the
/// Europe/Sarajevo offset; <c>totalSpaces</c> counts active spaces.
/// </summary>
public sealed record AvailabilityResponse(
    Guid LotId,
    DateTimeOffset Start,
    DateTimeOffset End,
    int TotalSpaces,
    int AvailableSpaces,
    IReadOnlyList<FreeSpaceResponse> FreeSpaces)
{
    public static AvailabilityResponse From(Guid lotId, DateTimeOffset start, DateTimeOffset end, AvailabilityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            lotId,
            start,
            end,
            result.TotalSpaces,
            result.AvailableSpaces,
            [.. result.FreeSpaces.Select(s => new FreeSpaceResponse(s.Id, s.Label))]);
    }
}

public sealed record FreeSpaceResponse(Guid Id, string Label);

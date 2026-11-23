using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Lots;

/// <summary>
/// The interval of an availability or quote query, as ISO 8601 instants with an offset (Z or +01:00).
/// A missing value is 400; grid, duration and opening-hours rules stay in the domain (422).
/// </summary>
public sealed class AvailabilityQuery
{
    // Nullable, so a missing parameter fails [Required] instead of binding failing with a generic message.
    [Required]
    [FromQuery(Name = "start")]
    public DateTimeOffset? Start { get; init; }

    [Required]
    [FromQuery(Name = "end")]
    public DateTimeOffset? End { get; init; }
}

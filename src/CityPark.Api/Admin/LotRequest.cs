using System.ComponentModel.DataAnnotations;

using CityPark.Application.Admin;

namespace CityPark.Api.Admin;

/// <summary>
/// Body for creating or updating a lot. Only the shape is checked here (400); a non-positive price,
/// a bad currency or a window that closes before it opens are domain rules (422).
/// </summary>
public sealed class LotRequest
{
    [Required]
    [StringLength(200)]
    public string? Name { get; init; }

    [Required]
    [StringLength(200)]
    public string? Address { get; init; }

    // The column is numeric(10,2); a larger value would fail in the database as a 500.
    [Required]
    [Range(typeof(decimal), "-99999999.99", "99999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? HourlyPrice { get; init; }

    [Required]
    public string? Currency { get; init; }

    /// <summary>Local Europe/Sarajevo time, for example "07:00:00".</summary>
    [Required]
    public TimeOnly? OpensAt { get; init; }

    [Required]
    public TimeOnly? ClosesAt { get; init; }

    // ValidationFilter has already rejected missing fields.
    public LotDetails ToDetails() =>
        new(Name!, Address!, HourlyPrice!.Value, Currency!, OpensAt!.Value, ClosesAt!.Value);
}

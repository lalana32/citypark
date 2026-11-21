using System.ComponentModel.DataAnnotations;

namespace CityPark.Api.Admin;

/// <summary>Body for adding a space. The label is trimmed and upper-cased, and must be unique in the lot.</summary>
public sealed class CreateSpaceRequest
{
    [Required]
    [StringLength(20)]
    public string? Label { get; init; }
}

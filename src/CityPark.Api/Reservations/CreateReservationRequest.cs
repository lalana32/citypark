using System.ComponentModel.DataAnnotations;

namespace CityPark.Api.Reservations;

/// <summary>
/// Body for creating a reservation. Only the shape is checked here (400); the business rules
/// R1–R14 stay in the domain (422).
/// </summary>
public sealed class CreateReservationRequest
{
    // Nullable, so a missing field fails [Required] instead of silently becoming Guid.Empty or 0001-01-01.
    [Required]
    public Guid? SpaceId { get; init; }

    [Required]
    public DateTimeOffset? Start { get; init; }

    [Required]
    public DateTimeOffset? End { get; init; }

    [Required]
    [StringLength(20)]
    public string? Plate { get; init; }
}

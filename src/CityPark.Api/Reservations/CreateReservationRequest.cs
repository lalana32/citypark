using System.ComponentModel.DataAnnotations;

namespace CityPark.Api.Reservations;

/// <summary>
/// Body for creating a reservation: exactly one of SpaceId (that space) or LotId (the first free
/// space of the lot). Only the shape is checked here (400); the business rules R1–R14 stay in the domain (422).
/// </summary>
public sealed class CreateReservationRequest : IValidatableObject
{
    public Guid? SpaceId { get; init; }

    public Guid? LotId { get; init; }

    // Nullable, so a missing field fails [Required] instead of silently becoming 0001-01-01.
    [Required]
    public DateTimeOffset? Start { get; init; }

    [Required]
    public DateTimeOffset? End { get; init; }

    [Required]
    [StringLength(20)]
    public string? Plate { get; init; }

    // Runs only after the attributes above pass.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SpaceId.HasValue == LotId.HasValue)
        {
            yield return new ValidationResult(
                "Send exactly one of spaceId and lotId.", [nameof(SpaceId), nameof(LotId)]);
        }
    }
}

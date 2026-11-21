using CityPark.Domain.Common;

namespace CityPark.Application.Admin;

/// <summary>A space still has Active reservations that have not ended, so it cannot be deactivated (R12, I8).</summary>
public sealed class SpaceHasActiveReservationsException : DomainConflictException
{
    public SpaceHasActiveReservationsException(Guid spaceId, IReadOnlyList<Guid> reservationIds)
        : base($"Space {spaceId} has {(reservationIds ?? throw new ArgumentNullException(nameof(reservationIds))).Count} active reservation(s) that have not ended; cancel them first.")
    {
        SpaceId = spaceId;
        ReservationIds = reservationIds;
    }

    public Guid SpaceId { get; }

    /// <summary>The reservations to cancel first, earliest start first.</summary>
    public IReadOnlyList<Guid> ReservationIds { get; }
}

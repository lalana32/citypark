namespace CityPark.Domain.Reservations;

/// <summary>What a customer sees: <see cref="ReservationStatus"/> combined with the current time. Never stored.</summary>
public enum ReservationState
{
    Upcoming = 1,
    InProgress = 2,
    Finished = 3,
    Cancelled = 4,
}

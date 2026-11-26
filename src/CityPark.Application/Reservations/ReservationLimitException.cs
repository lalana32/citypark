using CityPark.Domain.Common;

namespace CityPark.Application.Reservations;

/// <summary>The user already holds the maximum number of Active reservations that have not started yet (R10).</summary>
public sealed class ReservationLimitException : DomainConflictException
{
    public ReservationLimitException(int limit)
        : base($"You already have {limit} upcoming reservations, the most one user can hold. Cancel one to book another.")
    {
        Limit = limit;
    }

    public int Limit { get; }
}

using CityPark.Domain.Reservations;

namespace CityPark.Application.Admin;

/// <summary>One row of the admin reservation list: any user's reservation. Start and End are UTC (ADR-004).</summary>
public sealed record AdminReservation(
    Guid Id,
    Guid UserId,
    Guid LotId,
    string LotName,
    Guid SpaceId,
    string SpaceLabel,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Plate,
    decimal Price,
    string Currency,
    ReservationStatus Status,
    ReservationState State);

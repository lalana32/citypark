using CityPark.Domain.Reservations;

namespace CityPark.Application.Reservations;

/// <summary>One row of a customer's reservation list. Start and End are UTC (ADR-004).</summary>
public sealed record OwnReservation(
    Guid Id,
    string LotName,
    string SpaceLabel,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Plate,
    decimal Price,
    string Currency,
    ReservationState State);

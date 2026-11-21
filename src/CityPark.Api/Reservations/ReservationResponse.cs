using System.Text.Json.Serialization;

using CityPark.Application.Reservations;
using CityPark.Domain.Reservations;

namespace CityPark.Api.Reservations;

/// <summary>One of the caller's reservations. Start and End are UTC (ADR-004).</summary>
public sealed record ReservationResponse(
    Guid Id,
    string LotName,
    string SpaceLabel,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Plate,
    decimal Price,
    string Currency,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReservationState>))] ReservationState State)
{
    public static ReservationResponse From(OwnReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new(
            reservation.Id,
            reservation.LotName,
            reservation.SpaceLabel,
            reservation.Start,
            reservation.End,
            reservation.Plate,
            reservation.Price,
            reservation.Currency,
            reservation.State);
    }
}

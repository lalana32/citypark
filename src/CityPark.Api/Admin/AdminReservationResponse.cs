using System.Text.Json.Serialization;

using CityPark.Application.Admin;
using CityPark.Domain.Reservations;

namespace CityPark.Api.Admin;

/// <summary>Any user's reservation as the Admin role sees it. Start and End are UTC (ADR-004).</summary>
public sealed record AdminReservationResponse(
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
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReservationStatus>))] ReservationStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReservationState>))] ReservationState State)
{
    public static AdminReservationResponse From(AdminReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        return new(
            reservation.Id,
            reservation.UserId,
            reservation.LotId,
            reservation.LotName,
            reservation.SpaceId,
            reservation.SpaceLabel,
            reservation.Start,
            reservation.End,
            reservation.Plate,
            reservation.Price,
            reservation.Currency,
            reservation.Status,
            reservation.State);
    }
}

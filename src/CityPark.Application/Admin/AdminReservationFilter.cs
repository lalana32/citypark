using CityPark.Application.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Application.Admin;

/// <summary>
/// Optional filters of the admin reservation list. From and To are local Europe/Sarajevo dates (ADR-004),
/// both inclusive, and match the day the reservation starts on.
/// </summary>
public sealed record AdminReservationFilter
{
    public AdminReservationFilter(Guid? lotId = null, DateOnly? from = null, DateOnly? to = null, ReservationStatus? status = null)
    {
        // Enum binding also accepts numbers, so "status=7" would arrive as an undefined value.
        if (status is { } value && !Enum.IsDefined(value))
        {
            throw new RequestValidationException(
                $"Status {value} is unknown.",
                new Dictionary<string, string[]> { ["status"] = ["Status must be Active or Cancelled."] });
        }

        if (from > to)
        {
            throw new RequestValidationException(
                $"From ({from:yyyy-MM-dd}) is after to ({to:yyyy-MM-dd}).",
                new Dictionary<string, string[]> { ["to"] = ["To must be on or after from."] });
        }

        LotId = lotId;
        From = from;
        To = to;
        Status = status;
    }

    public Guid? LotId { get; }

    public DateOnly? From { get; }

    public DateOnly? To { get; }

    public ReservationStatus? Status { get; }
}

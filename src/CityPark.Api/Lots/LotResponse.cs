using CityPark.Application.Lots;

namespace CityPark.Api.Lots;

/// <summary>A parking lot. Opening hours are local Europe/Sarajevo times; totalSpaces counts active spaces.</summary>
public sealed record LotResponse(
    Guid Id,
    string Name,
    string Address,
    decimal HourlyPrice,
    string Currency,
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    int TotalSpaces)
{
    public static LotResponse From(LotSummary lot)
    {
        ArgumentNullException.ThrowIfNull(lot);

        return new(lot.Id, lot.Name, lot.Address, lot.HourlyPrice, lot.Currency, lot.OpensAt, lot.ClosesAt, lot.TotalSpaces);
    }
}

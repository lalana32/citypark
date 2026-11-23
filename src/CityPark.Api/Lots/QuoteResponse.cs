using CityPark.Application.Lots;

namespace CityPark.Api.Lots;

/// <summary>
/// The price a reservation of the lot for this interval would get. <c>start</c> and <c>end</c> are
/// echoed with the Europe/Sarajevo offset; <c>billedHours</c> counts every started hour (R8).
/// </summary>
public sealed record QuoteResponse(
    Guid LotId,
    DateTimeOffset Start,
    DateTimeOffset End,
    int BilledHours,
    decimal Price,
    string Currency)
{
    public static QuoteResponse From(Guid lotId, DateTimeOffset start, DateTimeOffset end, Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return new(lotId, start, end, quote.BilledHours, quote.Price, quote.Currency);
    }
}

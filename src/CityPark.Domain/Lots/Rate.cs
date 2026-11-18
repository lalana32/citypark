using CityPark.Domain.Common;

namespace CityPark.Domain.Lots;

/// <summary>
/// Price of one billed hour in a lot, for example 2.00 BAM.
/// </summary>
public sealed record Rate
{
    public Rate(decimal hourlyPrice, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (hourlyPrice <= 0)
        {
            throw new DomainException($"{nameof(HourlyPrice)} must be positive, but was {hourlyPrice}.");
        }

        if (decimal.Round(hourlyPrice, 2) != hourlyPrice)
        {
            throw new DomainException($"{nameof(HourlyPrice)} must have at most two decimals, but was {hourlyPrice}.");
        }

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"{nameof(Currency)} must be a three-letter upper-case ISO code, but was '{currency}'.");
        }

        HourlyPrice = hourlyPrice;
        Currency = currency;
    }

    public decimal HourlyPrice { get; }

    public string Currency { get; }
}

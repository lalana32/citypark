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
            throw new DomainException($"{nameof(HourlyPrice)} must be positive, but was {hourlyPrice}.")
            {
                Field = nameof(HourlyPrice),
            };
        }

        if (decimal.Round(hourlyPrice, 2) != hourlyPrice)
        {
            throw new DomainException($"{nameof(HourlyPrice)} must have at most two decimals, but was {hourlyPrice}.")
            {
                Field = nameof(HourlyPrice),
            };
        }

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"{nameof(Currency)} must be a three-letter upper-case ISO code, but was '{currency}'.")
            {
                Field = nameof(Currency),
            };
        }

        HourlyPrice = hourlyPrice;
        Currency = currency;
    }

    public decimal HourlyPrice { get; }

    public string Currency { get; }

    /// <summary>
    /// Hours billed for a stay of the given length: every started hour is a full hour (R8).
    /// 60 minutes is one hour, 61 minutes is two.
    /// </summary>
    public static int BilledHours(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException($"Duration must be positive, but was {duration}.");
        }

        // Integer ceiling on ticks: no floating point, so 60 minutes can never become 1.0000001 hours.
        return (int)((duration.Ticks + TimeSpan.TicksPerHour - 1) / TimeSpan.TicksPerHour);
    }

    /// <summary>
    /// Price of a stay of the given length, in <see cref="Currency"/>: <see cref="BilledHours"/> times the hourly price (R8).
    /// </summary>
    public decimal PriceFor(TimeSpan duration) =>
        // A two-decimal price times a whole number of hours always has two decimals; no rounding needed.
        HourlyPrice * BilledHours(duration);
}

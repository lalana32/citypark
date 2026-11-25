using System.Text;
using System.Text.RegularExpressions;

using CityPark.Domain.Common;

namespace CityPark.Domain.Reservations;

/// <summary>
/// A current Bosnia and Herzegovina license plate in normalized form, for example A12K345 (R14).
/// </summary>
public sealed record LicensePlate
{
    // [0-9] instead of \d: \d also matches non-ASCII digits such as Arabic-Indic ones.
    private static readonly Regex Format = new(
        "^[AEJKMOT][0-9]{2}[AEJKMOT][0-9]{3}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public LicensePlate(string value)
    {
        var normalized = Normalize(value);

        if (!Format.IsMatch(normalized))
        {
            throw new DomainException(
                $"License plate '{value}' does not match the Bosnia and Herzegovina format, for example A12K345.")
            {
                Field = "Plate",
            };
        }

        Value = normalized;
    }

    public string Value { get; }

    /// <summary>
    /// Trims, upper-cases, removes spaces and hyphens and converts the Cyrillic letters used on
    /// plates (А, Е, Ј, К, М, О, Т) to Latin. Does not check the format.
    /// </summary>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);

        foreach (var c in value.Trim().ToUpperInvariant())
        {
            if (c is ' ' or '-')
            {
                continue;
            }

            builder.Append(ToLatin(c));
        }

        return builder.ToString();
    }

    public override string ToString() => Value;

    // The seven Cyrillic letters look exactly like their Latin twins but are different characters,
    // so "А12К345" typed on a Cyrillic keyboard would otherwise never match the Latin pattern.
    private static char ToLatin(char c) => c switch
    {
        'А' => 'A', // А
        'Е' => 'E', // Е
        'Ј' => 'J', // Ј
        'К' => 'K', // К
        'М' => 'M', // М
        'О' => 'O', // О
        'Т' => 'T', // Т
        _ => c,
    };
}

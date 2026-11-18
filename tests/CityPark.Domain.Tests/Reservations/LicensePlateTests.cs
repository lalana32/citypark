using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

namespace CityPark.Domain.Tests.Reservations;

public class LicensePlateTests
{
    [Theory]
    [InlineData(" ab-123 ", "AB123")]
    [InlineData("a12-k-345", "A12K345")]
    [InlineData("A12 K 345", "A12K345")]
    [InlineData("A12K345", "A12K345")]
    public void Normalize_TrimsUpperCasesAndRemovesSpacesAndHyphens(string input, string expected)
    {
        Assert.Equal(expected, LicensePlate.Normalize(input));
    }

    [Fact]
    public void Normalize_ConvertsCyrillicLettersToLatin()
    {
        // Lower-case Cyrillic а and к: upper-cased to А and К, then converted to Latin A and K.
        var cyrillic = "а" + "12-" + "к" + "-345";

        Assert.Equal("A12K345", LicensePlate.Normalize(cyrillic));
    }

    [Fact]
    public void Normalize_WhenValueIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LicensePlate.Normalize(null!));
    }

    [Theory]
    [InlineData("a12-k-345")]
    [InlineData("Е" + "12 " + "Т" + " 345")] // Cyrillic Е and Т
    [InlineData("O99M000")]
    public void Constructor_WithValidPlate_StoresNormalizedValue(string input)
    {
        var plate = new LicensePlate(input);

        Assert.Equal(LicensePlate.Normalize(input), plate.Value);
    }

    [Theory]
    [InlineData("B12-K-345")] // B is not one of the seven allowed letters
    [InlineData("A1-K-345")] // too few digits
    [InlineData("A12-K-3456")] // too many digits
    [InlineData(" ab-123 ")] // normalizes fine, but is not the BiH format
    [InlineData("A12K34٥")] // Arabic-Indic digit five, matched by \d but not by [0-9]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidPlate_ThrowsDomainException(string input)
    {
        Assert.Throws<DomainException>(() => new LicensePlate(input));
    }

    [Fact]
    public void Plates_WithSameNormalizedValue_AreEqual()
    {
        Assert.Equal(new LicensePlate("a12-k-345"), new LicensePlate("A12K345"));
    }
}

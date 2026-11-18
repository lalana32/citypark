using CityPark.Domain.Common;
using CityPark.Domain.Lots;

namespace CityPark.Domain.Tests.Lots;

public class RateTests
{
    [Fact]
    public void Constructor_WithValidValues_ExposesPriceAndCurrency()
    {
        var rate = new Rate(2.00m, "BAM");

        Assert.Equal(2.00m, rate.HourlyPrice);
        Assert.Equal("BAM", rate.Currency);
    }

    [Fact]
    public void Constructor_WhenPriceIsZero_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Rate(0m, "BAM"));
    }

    [Fact]
    public void Constructor_WhenPriceIsNegative_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Rate(-2.00m, "BAM"));
    }

    [Fact]
    public void Constructor_WithSmallestPositivePrice_IsValid()
    {
        var rate = new Rate(0.01m, "BAM");

        Assert.Equal(0.01m, rate.HourlyPrice);
    }

    [Fact]
    public void Constructor_WhenPriceHasMoreThanTwoDecimals_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Rate(2.005m, "BAM"));
    }

    [Fact]
    public void Constructor_WhenTrailingZerosGoBeyondTwoDecimals_IsValid()
    {
        // 2.000 has scale 3 but the same value as 2.00, so it is not rejected.
        var rate = new Rate(2.000m, "BAM");

        Assert.Equal(2.00m, rate.HourlyPrice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BA")]
    [InlineData("BAMX")]
    [InlineData("bam")]
    [InlineData("B4M")]
    [InlineData(" BAM")]
    public void Constructor_WhenCurrencyIsNotThreeUpperCaseLetters_ThrowsDomainException(string currency)
    {
        Assert.Throws<DomainException>(() => new Rate(2.00m, currency));
    }

    [Fact]
    public void Constructor_WhenCurrencyIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Rate(2.00m, null!));
    }

    [Fact]
    public void Equality_WhenPriceAndCurrencyAreEqual_RatesAreEqual()
    {
        Assert.Equal(new Rate(2.00m, "BAM"), new Rate(2.0m, "BAM"));
    }

    [Fact]
    public void Equality_WhenCurrencyDiffers_RatesAreNotEqual()
    {
        Assert.NotEqual(new Rate(2.00m, "BAM"), new Rate(2.00m, "EUR"));
    }
}

using CityPark.Application.Common;

namespace CityPark.Application.Tests.Common;

public class PageRequestTests
{
    [Fact]
    public void Constructor_ValidValues_ComputesSkip()
    {
        var request = new PageRequest(3, 20);

        Assert.Equal(3, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(40, request.Skip);
    }

    [Theory]
    [InlineData(50, 50)]
    [InlineData(51, 50)]
    [InlineData(100, 50)]
    public void Constructor_PageSizeAboveMaximum_IsClamped(int requested, int expected)
    {
        var request = new PageRequest(2, requested);

        Assert.Equal(expected, request.PageSize);
        Assert.Equal(expected, request.Skip); // the offset uses the clamped size
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_PageBelowOne_Throws(int page)
    {
        Assert.Throws<RequestValidationException>(() => new PageRequest(page, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_PageSizeBelowOne_Throws(int pageSize)
    {
        Assert.Throws<RequestValidationException>(() => new PageRequest(1, pageSize));
    }

    [Fact]
    public void Constructor_PageWhoseOffsetOverflowsInt_Throws()
    {
        Assert.Throws<RequestValidationException>(() => new PageRequest(int.MaxValue, 50));
    }
}

public class PagedResultTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    public void TotalPages_RoundsUp(int totalCount, int expected)
    {
        var result = new PagedResult<int>([], 1, 20, totalCount);

        Assert.Equal(expected, result.TotalPages);
    }
}

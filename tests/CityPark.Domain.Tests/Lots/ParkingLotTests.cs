using CityPark.Domain.Common;
using CityPark.Domain.Lots;

namespace CityPark.Domain.Tests.Lots;

public class ParkingLotTests
{
    private static readonly Rate TwoMarks = new(2.00m, "BAM");

    private static readonly OpeningHours DayWindow = new(new TimeOnly(7, 0), new TimeOnly(22, 0));

    private static ParkingLot NewLot() => ParkingLot.Create("Centar", "Titova 1", TwoMarks, DayWindow);

    [Fact]
    public void Create_WithValidValues_ExposesValuesAndHasNoSpaces()
    {
        var lot = ParkingLot.Create("Centar", "Titova 1", TwoMarks, DayWindow);

        Assert.NotEqual(Guid.Empty, lot.Id);
        Assert.Equal("Centar", lot.Name);
        Assert.Equal("Titova 1", lot.Address);
        Assert.Equal(TwoMarks, lot.Rate);
        Assert.Equal(DayWindow, lot.OpeningHours);
        Assert.Empty(lot.Spaces);
    }

    [Fact]
    public void LotAndSpace_GetVersion7Ids()
    {
        var lot = NewLot();
        var space = lot.AddSpace("A-1");

        Assert.Equal(7, lot.Id.Version);
        Assert.Equal(7, space.Id.Version);
    }

    [Fact]
    public void Create_TrimsNameAndAddress()
    {
        var lot = ParkingLot.Create("  Centar ", " Titova 1  ", TwoMarks, DayWindow);

        Assert.Equal("Centar", lot.Name);
        Assert.Equal("Titova 1", lot.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameIsBlank_ThrowsDomainException(string name)
    {
        Assert.Throws<DomainException>(() => ParkingLot.Create(name, "Titova 1", TwoMarks, DayWindow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenAddressIsBlank_ThrowsDomainException(string address)
    {
        Assert.Throws<DomainException>(() => ParkingLot.Create("Centar", address, TwoMarks, DayWindow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Create_WithZeroOrNegativeHourlyPrice_IsRejected(int price)
    {
        // A lot cannot even be given such a rate: the Rate constructor rejects it.
        Assert.Throws<DomainException>(() => ParkingLot.Create("Centar", "Titova 1", new Rate(price, "BAM"), DayWindow));
    }

    [Fact]
    public void Create_WhenRateIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParkingLot.Create("Centar", "Titova 1", null!, DayWindow));
    }

    [Fact]
    public void Create_WhenOpeningHoursAreNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ParkingLot.Create("Centar", "Titova 1", TwoMarks, null!));
    }

    [Fact]
    public void AddSpace_AddsActiveSpaceToTheLot()
    {
        var lot = NewLot();

        var space = lot.AddSpace("A-1");

        Assert.Equal("A-1", space.Label);
        Assert.True(space.IsActive);
        Assert.Same(space, Assert.Single(lot.Spaces));
    }

    [Fact]
    public void AddSpace_WithDifferentLabels_AddsAllSpaces()
    {
        var lot = NewLot();

        lot.AddSpace("A-1");
        lot.AddSpace("A-2");

        Assert.Equal(2, lot.Spaces.Count);
    }

    [Fact]
    public void AddSpace_NormalizesLabelToTrimmedUpperCase()
    {
        var space = NewLot().AddSpace("  a-1 ");

        Assert.Equal("A-1", space.Label);
    }

    [Fact]
    public void AddSpace_WhenLabelAlreadyExists_ThrowsDomainException()
    {
        var lot = NewLot();
        lot.AddSpace("A-1");

        Assert.Throws<DomainException>(() => lot.AddSpace("A-1"));
    }

    [Theory]
    [InlineData("a-1")]
    [InlineData(" A-1 ")]
    public void AddSpace_WhenLabelDiffersOnlyInCaseOrSpaces_ThrowsDomainException(string duplicate)
    {
        var lot = NewLot();
        lot.AddSpace("A-1");

        Assert.Throws<DomainException>(() => lot.AddSpace(duplicate));
        Assert.Single(lot.Spaces);
    }

    [Fact]
    public void AddSpace_SameLabelInDifferentLots_IsAllowed()
    {
        var first = NewLot().AddSpace("A-1");
        var second = NewLot().AddSpace("A-1");

        Assert.Equal(first.Label, second.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSpace_WhenLabelIsBlank_ThrowsDomainException(string label)
    {
        Assert.Throws<DomainException>(() => NewLot().AddSpace(label));
    }

    [Fact]
    public void Spaces_CannotBeModifiedFromOutside()
    {
        var lot = NewLot();
        var spaces = (ICollection<ParkingSpace>)lot.Spaces;

        Assert.Throws<NotSupportedException>(() => spaces.Clear());
    }
}

using CityPark.Domain.Common;

namespace CityPark.Domain.Tests.Common;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ExposesMessage()
    {
        var exception = new DomainException("End must be after start.");

        Assert.Equal("End must be after start.", exception.Message);
        Assert.Null(exception.Field);
    }

    [Fact]
    public void DomainConflictException_IsADomainException()
    {
        DomainException exception = new DomainConflictException("Reservation is already cancelled.");

        Assert.IsType<DomainConflictException>(exception);
        Assert.Equal("Reservation is already cancelled.", exception.Message);
    }
}

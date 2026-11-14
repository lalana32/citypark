using System.Reflection;

namespace CityPark.Infrastructure.Tests;

public class PlaceholderTests
{
    [Fact]
    public void ReferencedAssembly_CanBeLoaded()
    {
        var assembly = Assembly.Load("CityPark.Infrastructure");

        Assert.Equal("CityPark.Infrastructure", assembly.GetName().Name);
    }
}

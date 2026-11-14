using System.Reflection;

namespace CityPark.Api.Tests;

public class PlaceholderTests
{
    [Fact]
    public void ReferencedAssembly_CanBeLoaded()
    {
        var assembly = Assembly.Load("CityPark.Api");

        Assert.Equal("CityPark.Api", assembly.GetName().Name);
    }
}

using System.Reflection;

namespace CityPark.Application.Tests;

public class PlaceholderTests
{
    [Fact]
    public void ReferencedAssembly_CanBeLoaded()
    {
        var assembly = Assembly.Load("CityPark.Application");

        Assert.Equal("CityPark.Application", assembly.GetName().Name);
    }
}

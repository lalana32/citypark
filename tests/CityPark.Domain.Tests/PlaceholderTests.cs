using System.Reflection;

namespace CityPark.Domain.Tests;

public class PlaceholderTests
{
    [Fact]
    public void ReferencedAssembly_CanBeLoaded()
    {
        var assembly = Assembly.Load("CityPark.Domain");

        Assert.Equal("CityPark.Domain", assembly.GetName().Name);
    }
}

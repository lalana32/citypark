using CityPark.Application.Persistence;

namespace CityPark.Application.Tests.Architecture;

public class DependencyTests
{
    private static readonly string[] ReferencedAssemblies =
        [.. typeof(IAppDbContext).Assembly.GetReferencedAssemblies().Select(a => a.Name!)];

    [Fact]
    public void Application_UsesCoreEfCore()
    {
        // Guards the test below: if Application stopped using EF Core at all, "no provider" would pass trivially.
        Assert.Contains("Microsoft.EntityFrameworkCore", ReferencedAssemblies);
    }

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("CityPark.Infrastructure")]
    public void Application_DoesNotReferenceProviderOrInfrastructure(string assembly)
    {
        Assert.DoesNotContain(assembly, ReferencedAssemblies);
    }
}

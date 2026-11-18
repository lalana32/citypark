using CityPark.Application.Persistence;
using CityPark.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace CityPark.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void IAppDbContext_ResolvesToTheSameScopedAppDbContext()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure("Host=unused")
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var abstraction = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var concrete = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Same(concrete, abstraction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_WithoutConnectionString_Throws(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddInfrastructure(connectionString));
    }
}

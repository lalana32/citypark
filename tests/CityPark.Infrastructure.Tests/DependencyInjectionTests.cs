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

    [Fact]
    public void IDatabaseErrors_ResolvesToThePostgresImplementation()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure("Host=unused")
            .BuildServiceProvider(validateScopes: true);

        Assert.IsType<PostgresDatabaseErrors>(provider.GetRequiredService<IDatabaseErrors>());
    }

    [Fact]
    public void AppDbContext_RetriesTransientFailures()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure("Host=unused")
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_WithoutConnectionString_Throws(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddInfrastructure(connectionString));
    }
}

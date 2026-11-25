using System.Reflection;

using CityPark.Application;
using CityPark.Domain.Lots;

using NetArchTest.Rules;

namespace CityPark.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(ParkingLot).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    private static readonly string[] OuterLayersOfDomain =
        ["CityPark.Application", "CityPark.Infrastructure", "CityPark.Api"];

    private static readonly string[] OuterLayersOfApplication =
        ["CityPark.Infrastructure", "CityPark.Api"];

    private static readonly string[] FrameworksForbiddenInDomain =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"];

    // Application may use core EF Core (DbSet<T> in IAppDbContext, ADR-001), but no provider.
    private static readonly string[] FrameworksForbiddenInApplication =
        ["Npgsql", "Microsoft.AspNetCore"];

    [Fact]
    public void Domain_DoesNotDependOnOtherLayers()
    {
        AssertNoDependency(DomainAssembly, OuterLayersOfDomain);
    }

    [Fact]
    public void Domain_DoesNotDependOnEfCoreOrAspNetCore()
    {
        AssertNoDependency(DomainAssembly, FrameworksForbiddenInDomain);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        AssertNoDependency(ApplicationAssembly, OuterLayersOfApplication);
    }

    [Fact]
    public void Application_DoesNotDependOnEfCoreProviderOrAspNetCore()
    {
        AssertNoDependency(ApplicationAssembly, FrameworksForbiddenInApplication);
    }

    // Provider extension methods such as UseNpgsql live in the Microsoft.EntityFrameworkCore
    // namespace, so the namespace rules miss them; the assembly reference does not.
    [Theory]
    [InlineData("CityPark.Domain")]
    [InlineData("CityPark.Application")]
    public void Layer_DoesNotReferenceEfCoreProviderOrAspNetCoreAssemblies(string layer)
    {
        var forbidden = Assembly.Load(layer)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("Npgsql", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

        Assert.Empty(forbidden);
    }

    private static void AssertNoDependency(Assembly layer, string[] forbidden)
    {
        var result = Types.InAssembly(layer)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{layer.GetName().Name} must not depend on {string.Join(", ", forbidden)}. Failing types: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}

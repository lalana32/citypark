namespace CityPark.Api.Tests.OpenApi;

/// <summary>The API without the test-only routes, so its OpenAPI document is the real contract.</summary>
public sealed class OpenApiContractFactory : ApiFactory
{
    protected override bool AddTestEndpoints => false;
}

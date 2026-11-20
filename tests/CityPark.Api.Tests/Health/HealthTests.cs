using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CityPark.Api.Tests.Health;

public class HealthWithoutDatabaseTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_Returns200_EvenWhenDatabaseIsUnreachable()
    {
        var (status, body) = await HealthClient.GetAsync(factory, "/health/live");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_Returns503_WhenDatabaseIsUnreachable()
    {
        var (status, body) = await HealthClient.GetAsync(factory, "/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("database").GetString());
    }
}

public class HealthWithDatabaseTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Ready_Returns200_WhenDatabaseAnswers()
    {
        var (status, body) = await HealthClient.GetAsync(factory, "/health/ready");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }
}

internal static class HealthClient
{
    public static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(ApiFactory factory, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);

        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>(ct));
    }
}

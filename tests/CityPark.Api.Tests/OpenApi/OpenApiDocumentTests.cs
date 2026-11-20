using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CityPark.Api.Tests.OpenApi;

public class OpenApiDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Document_IsServed_AndListsHealthEndpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/health/live", out _));
        Assert.True(paths.TryGetProperty("/health/ready", out var ready));
        Assert.True(ready.GetProperty("get").GetProperty("responses").TryGetProperty("503", out _));
    }

    [Fact]
    public async Task ScalarUi_IsNotServed_InProduction()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

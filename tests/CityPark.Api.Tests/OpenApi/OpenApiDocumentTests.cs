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

    [Theory]
    [InlineData("/api/v1/lots", "items")]
    [InlineData("/api/v1/lots/{id}", "totalSpaces")]
    [InlineData("/api/v1/lots/{id}/availability", "freeSpaces")]
    public async Task LotEndpoints_HaveA200Example(string path, string exampleProperty)
    {
        var get = await GetOperationAsync(path);

        var example = get.GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("example");
        Assert.True(example.TryGetProperty(exampleProperty, out _));
    }

    [Fact]
    public async Task Availability_HasParameterAnd422Examples()
    {
        var get = await GetOperationAsync("/api/v1/lots/{id}/availability");

        var parameters = get.GetProperty("parameters").EnumerateArray().ToList();
        foreach (var name in new[] { "start", "end" })
        {
            var parameter = parameters.Single(p => p.GetProperty("name").GetString() == name);
            Assert.True(parameter.GetProperty("required").GetBoolean());
            Assert.Equal(JsonValueKind.String, parameter.GetProperty("example").ValueKind);
        }

        var problem = get.GetProperty("responses").GetProperty("422")
            .GetProperty("content").GetProperty("application/problem+json").GetProperty("example");
        Assert.True(problem.GetProperty("errors").TryGetProperty("start", out _));
    }

    [Fact]
    public async Task ScalarUi_IsNotServed_InProduction()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> GetOperationAsync(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), ct);

        return document.GetProperty("paths").GetProperty(path).GetProperty("get");
    }
}

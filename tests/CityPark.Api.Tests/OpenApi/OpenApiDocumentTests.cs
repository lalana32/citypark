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

    [Theory]
    [InlineData("/api/v1/reservations", "post", new[] { "201", "400", "401", "404", "409", "422" })]
    [InlineData("/api/v1/reservations", "get", new[] { "200", "400", "401" })]
    [InlineData("/api/v1/reservations/{id}", "get", new[] { "200", "401", "404" })]
    [InlineData("/api/v1/reservations/{id}/cancel", "post", new[] { "204", "401", "404", "409", "422" })]
    public async Task ReservationEndpoints_DeclareEveryStatusCode(string path, string method, string[] statuses)
    {
        var operation = await GetOperationAsync(path, method);

        var declared = operation.GetProperty("responses").EnumerateObject().Select(r => r.Name);
        Assert.Subset(declared.ToHashSet(), statuses.ToHashSet());
    }

    [Theory]
    [InlineData("/api/v1/admin/lots", "post", new[] { "201", "400", "401", "403", "422" })]
    [InlineData("/api/v1/admin/lots/{id}", "put", new[] { "200", "400", "401", "403", "404", "422" })]
    [InlineData("/api/v1/admin/lots/{id}/spaces", "post", new[] { "201", "400", "401", "403", "404", "409", "422" })]
    [InlineData("/api/v1/admin/lots/{lotId}/spaces/{spaceId}/deactivate", "post", new[] { "204", "401", "403", "404", "409" })]
    [InlineData("/api/v1/admin/reservations", "get", new[] { "200", "400", "401", "403" })]
    [InlineData("/api/v1/admin/reservations/{id}/cancel", "post", new[] { "200", "401", "403", "404", "409" })]
    public async Task AdminEndpoints_AreTaggedAdminAndDeclareEveryStatusCode(string path, string method, string[] statuses)
    {
        var operation = await GetOperationAsync(path, method);

        Assert.Equal("Admin", Assert.Single(operation.GetProperty("tags").EnumerateArray()).GetString());
        var declared = operation.GetProperty("responses").EnumerateObject().Select(r => r.Name);
        Assert.Subset(declared.ToHashSet(), statuses.ToHashSet());
    }

    [Fact]
    public async Task DeactivateSpace_Has409ExampleListingReservations()
    {
        var post = await GetOperationAsync("/api/v1/admin/lots/{lotId}/spaces/{spaceId}/deactivate", "post");

        var example = post.GetProperty("responses").GetProperty("409")
            .GetProperty("content").GetProperty("application/problem+json").GetProperty("example");
        Assert.Single(example.GetProperty("reservationIds").EnumerateArray());
    }

    [Fact]
    public async Task CreateReservation_HasA201ExampleWithThePrice()
    {
        var post = await GetOperationAsync("/api/v1/reservations", "post");

        var example = post.GetProperty("responses").GetProperty("201")
            .GetProperty("content").GetProperty("application/json").GetProperty("example");
        Assert.Equal(5.00m, example.GetProperty("price").GetDecimal());
        Assert.Equal("Upcoming", example.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ScalarUi_IsNotServed_InProduction()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement> GetOperationAsync(string path, string method = "get")
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(new Uri("/openapi/v1.json", UriKind.Relative), ct);

        return document.GetProperty("paths").GetProperty(path).GetProperty(method);
    }
}

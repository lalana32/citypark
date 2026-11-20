using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CityPark.Api.Tests.Validation;

public class RuleViolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string UnprocessableEntityType = "https://tools.ietf.org/html/rfc9110#section-15.5.21";

    // 1 December 2026 is winter time in Sarajevo (UTC+1): 09:10Z is 10:10 local, off the grid.
    [Theory]
    [InlineData("2026-12-01T09:10:00Z", "2026-12-01T10:15:00Z", "start")]
    [InlineData("2026-12-01T09:00:00Z", "2026-12-01T10:10:00Z", "end")]
    public async Task OffGridTime_Returns422WithFieldNamingTheRule(string start, string end, string field)
    {
        var (status, mediaType, problem) = await PostAsync(Body(start, end, "A12K345"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("application/problem+json", mediaType);
        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal(UnprocessableEntityType, problem.GetProperty("type").GetString());
        Assert.Equal(ValidationEndpoints.BookableReservations, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(field, error.Name);
        var message = Assert.Single(error.Value.EnumerateArray()).GetString();
        Assert.Contains("15-minute grid", message, StringComparison.Ordinal);
        Assert.Contains("(R1)", message, StringComparison.Ordinal);
    }

    // The shape is checked before any rule, so a malformed request never reaches the domain.
    [Fact]
    public async Task EmptyPlateAndOffGridStart_Returns400NotThe422()
    {
        var (status, _, problem) = await PostAsync(Body("2026-12-01T09:10:00Z", "2026-12-01T10:15:00Z", ""));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("plate", Assert.Single(problem.GetProperty("errors").EnumerateObject()).Name);
    }

    private static string Body(string start, string end, string plate) =>
        new JsonObject
        {
            ["spaceId"] = "0192f3c4-0000-7000-8000-000000000001",
            ["start"] = start,
            ["end"] = end,
            ["plate"] = plate,
        }.ToJsonString();

    private async Task<(HttpStatusCode Status, string? MediaType, JsonElement Problem)> PostAsync(string json)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri(ValidationEndpoints.BookableReservations, UriKind.Relative), content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, JsonSerializer.Deserialize<JsonElement>(body));
    }
}

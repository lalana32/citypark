using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CityPark.Api.Tests.Validation;

public class RequestValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private const string ValidBody = """
        {
          "spaceId": "0192f3c4-0000-7000-8000-000000000001",
          "start": "2026-12-01T09:00:00Z",
          "end": "2026-12-01T10:00:00Z",
          "plate": "A12K345"
        }
        """;

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var (status, _, _) = await PostAsync(ValidBody);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmptyPlate_Returns400WithPlateError(string? plate)
    {
        var body = ValidObject();
        body["plate"] = plate;

        var errors = await PostExpectingBadRequestAsync(body.ToJsonString());

        Assert.Equal("plate", Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task LotIdInsteadOfSpaceId_PassesValidation()
    {
        var body = ValidObject();
        body.Remove("spaceId");
        body["lotId"] = "0192f3c4-0000-7000-8000-000000000002";

        var (status, _, _) = await PostAsync(body.ToJsonString());

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("plate")]
    public async Task MissingRequiredField_Returns400WithThatField(string field)
    {
        var body = ValidObject();
        body.Remove(field);

        var errors = await PostExpectingBadRequestAsync(body.ToJsonString());

        Assert.Equal(field, Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task EmptyObject_Returns400WithEveryRequiredField()
    {
        var errors = await PostExpectingBadRequestAsync("{}");

        // No spaceId/lotId error yet: the one-of check runs only after the required fields pass.
        var fields = errors.EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal);
        Assert.Equal("end,plate,start", string.Join(',', fields));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("""{ "plate": }""")]
    [InlineData("")]
    [InlineData("null")]
    public async Task MalformedOrMissingBody_Returns400(string json)
    {
        var errors = await PostExpectingBadRequestAsync(json);

        Assert.NotEmpty(errors.EnumerateObject());
    }

    [Theory]
    [InlineData("spaceId", "\"not-a-guid\"")]
    [InlineData("lotId", "\"not-a-guid\"")]
    [InlineData("start", "42")]
    [InlineData("end", "\"tomorrow\"")]
    [InlineData("plate", "true")]
    public async Task WrongType_Returns400WithThatField(string field, string value)
    {
        var body = ValidObject();
        body[field] = JsonNode.Parse(value);

        var errors = await PostExpectingBadRequestAsync(body.ToJsonString());

        Assert.Equal(field, Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task UnknownProperty_IsIgnored()
    {
        var body = ValidObject();
        body["colour"] = "red";
        body["extra"] = new JsonObject { ["nested"] = 1 };

        var (status, _, _) = await PostAsync(body.ToJsonString());

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task WrongContentType_Returns415ProblemDetails()
    {
        var (status, mediaType, _) = await PostAsync(ValidBody, "text/plain");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, status);
        Assert.Equal("application/problem+json", mediaType);
    }

    private static JsonObject ValidObject() => JsonNode.Parse(ValidBody)!.AsObject();

    private async Task<(HttpStatusCode Status, string? MediaType, string Body)> PostAsync(
        string json,
        string mediaType = "application/json")
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, mediaType);

        using var response = await client.PostAsync(new Uri(ValidationEndpoints.Reservations, UriKind.Relative), content, ct);

        return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Checks the shared 400 shape and returns its <c>errors</c> object.</summary>
    private async Task<JsonElement> PostExpectingBadRequestAsync(string json)
    {
        var (status, mediaType, body) = await PostAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("application/problem+json", mediaType);

        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(BadRequestType, problem.GetProperty("type").GetString());
        Assert.Equal(ValidationEndpoints.Reservations, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        return problem.GetProperty("errors");
    }
}

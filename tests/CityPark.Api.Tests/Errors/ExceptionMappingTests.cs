using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CityPark.Api.Tests.Errors;

public class ExceptionMappingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("request-validation", 400, "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
    [InlineData("not-found", 404, "https://tools.ietf.org/html/rfc9110#section-15.5.5")]
    [InlineData("domain-conflict", 409, "https://tools.ietf.org/html/rfc9110#section-15.5.10")]
    [InlineData("reservation-conflict", 409, "https://tools.ietf.org/html/rfc9110#section-15.5.10")]
    [InlineData("domain", 422, "https://tools.ietf.org/html/rfc9110#section-15.5.21")]
    [InlineData("unexpected", 500, "https://tools.ietf.org/html/rfc9110#section-15.6.1")]
    public async Task Exception_IsMappedToStatusAndProblemType(string kind, int status, string type)
    {
        var (actualStatus, problem) = await SendAsync(kind);

        Assert.Equal(status, (int)actualStatus);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal(type, problem.GetProperty("type").GetString());
        Assert.Equal($"{ThrowingEndpoints.Prefix}/{kind}", problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("domain", "A reservation must last at least 30 minutes.")]
    [InlineData("request-validation", "Page must be 1 or greater, but is 0.")]
    public async Task ValidationError_HasErrorsObject(string kind, string message)
    {
        var (_, problem) = await SendAsync(kind);

        var general = problem.GetProperty("errors").GetProperty("general");
        Assert.Equal(message, Assert.Single(general.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task ReservationConflict_DetailNamesTheSpace()
    {
        var (_, problem) = await SendAsync("reservation-conflict");

        Assert.Contains(Guid.Empty.ToString(), problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedError_InProduction_HidesExceptionDetails()
    {
        var (_, problem) = await SendAsync("unexpected");
        var body = problem.GetRawText();

        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.DoesNotContain(ThrowingEndpoints.SecretMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-such-route", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<(HttpStatusCode Status, JsonElement Problem)> SendAsync(string kind)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"{ThrowingEndpoints.Prefix}/{kind}", UriKind.Relative), ct);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return (response.StatusCode, problem);
    }
}

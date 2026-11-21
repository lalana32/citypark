using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Auth;

namespace CityPark.Api.Tests.Auth;

// Unknown emails: every attempt is a 401 until the limit, and no account is locked on the way.
public class RateLimitingTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly Uri Lots = new("/api/v1/lots", UriKind.Relative);

    [Fact]
    public async Task Login_EleventhAttemptWithinTheWindow_Returns429WithRetryAfter()
    {
        using var client = AuthClient.Create(factory);
        await ExhaustLoginBudgetAsync(client);

        using var response = await FailedLoginAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), AuthRateLimiting.Window);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Register_EleventhAttemptWithinTheWindow_Returns429()
    {
        using var client = AuthClient.Create(factory);
        for (var attempt = 1; attempt <= AuthRateLimiting.PermitLimit; attempt++)
        {
            using var allowed = await AuthClient.RegisterAsync(client, AuthClient.NewEmail(), "weak");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, allowed.StatusCode);
        }

        using var response = await AuthClient.RegisterAsync(client, AuthClient.NewEmail(), "weak");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_AnotherClient_IsNotLimitedByTheFirst()
    {
        using var first = AuthClient.Create(factory);
        using var second = AuthClient.Create(factory);
        await ExhaustLoginBudgetAsync(first);

        using var response = await FailedLoginAsync(second);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLots_IsNotLimited_EvenAfterTheLoginBudgetIsUsedUp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await ExhaustLoginBudgetAsync(client);

        for (var request = 1; request <= AuthRateLimiting.PermitLimit + 5; request++)
        {
            using var response = await client.GetAsync(Lots, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task ExhaustLoginBudgetAsync(HttpClient client)
    {
        for (var attempt = 1; attempt <= AuthRateLimiting.PermitLimit; attempt++)
        {
            using var response = await FailedLoginAsync(client);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> FailedLoginAsync(HttpClient client) =>
        AuthClient.LoginAsync(client, AuthClient.NewEmail(), "Not-The-Password-1");
}

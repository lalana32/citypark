using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using CityPark.Api.Auth;
using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;

namespace CityPark.Api.Tests.Errors;

// Each status of the error contract produced by a real endpoint, not by a throwing test route.
public class ErrorContractTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static readonly Uri Reservations = new("/api/v1/reservations", UriKind.Relative);

    private static readonly string[] LeakMarkers =
        ["   at ", ".cs:line", "Exception", "Npgsql", "Host=", "Username=", "Password="];

    public static TheoryData<string, int> Scenarios => new()
    {
        { "malformed-json", 400 },
        { "missing-csrf-token", 400 },
        { "invalid-page", 400 },
        { "anonymous", 401 },
        { "wrong-password", 401 },
        { "customer-on-admin-route", 403 },
        { "unknown-lot", 404 },
        { "duplicate-email", 409 },
        { "overlapping-reservation", 409 },
        { "weak-password", 422 },
        { "off-grid-reservation", 422 },
        { "too-many-logins", 429 },
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task ErrorResponse_IsProblemDetailsWithoutInternals(string scenario, int status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);

        using var response = await SendAsync(scenario, client, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.StartsWith("https://tools.ietf.org/html/rfc", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.Equal(response.RequestMessage?.RequestUri?.AbsolutePath, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        foreach (var marker in LeakMarkers)
        {
            Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
        }
    }

    private Task<HttpResponseMessage> SendAsync(string scenario, HttpClient client, CancellationToken ct) => scenario switch
    {
        "malformed-json" => MalformedJsonAsync(client, ct),
        "missing-csrf-token" => MissingCsrfTokenAsync(client, ct),
        "invalid-page" => client.GetAsync(Relative("/api/v1/lots?page=0"), ct),
        "anonymous" => client.GetAsync(AuthClient.Me, ct),
        "wrong-password" => AuthClient.LoginAsync(client, AuthClient.NewEmail(), AuthClient.Password),
        "customer-on-admin-route" => CustomerOnAdminRouteAsync(client, ct),
        "unknown-lot" => client.GetAsync(Relative($"/api/v1/lots/{Guid.CreateVersion7()}"), ct),
        "duplicate-email" => DuplicateEmailAsync(client),
        "overlapping-reservation" => OverlappingReservationAsync(client, ct),
        "weak-password" => AuthClient.RegisterAsync(client, AuthClient.NewEmail(), "weak"),
        "off-grid-reservation" => OffGridReservationAsync(client, ct),
        "too-many-logins" => TooManyLoginsAsync(client),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario."),
    };

    private static async Task<HttpResponseMessage> MalformedJsonAsync(HttpClient client, CancellationToken ct)
    {
        await AuthClient.SignInNewCustomerAsync(client);
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        return await client.PostAsync(Reservations, content, ct);
    }

    private static async Task<HttpResponseMessage> MissingCsrfTokenAsync(HttpClient client, CancellationToken ct)
    {
        await AuthClient.SignInNewCustomerAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, Reservations)
        {
            Content = JsonContent.Create(Body(Guid.CreateVersion7(), "2026-12-01T09:00:00Z", "2026-12-01T10:00:00Z", "A12K345")),
        };
        request.Options.Set(CsrfHandler.Skip, true);

        return await client.SendAsync(request, ct);
    }

    private static async Task<HttpResponseMessage> CustomerOnAdminRouteAsync(HttpClient client, CancellationToken ct)
    {
        await AuthClient.SignInNewCustomerAsync(client);

        return await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);
    }

    private static async Task<HttpResponseMessage> DuplicateEmailAsync(HttpClient client)
    {
        var email = AuthClient.NewEmail();
        using (var first = await AuthClient.RegisterAsync(client, email, AuthClient.Password))
        {
            first.EnsureSuccessStatusCode();
        }

        return await AuthClient.RegisterAsync(client, email, AuthClient.Password);
    }

    private async Task<HttpResponseMessage> OverlappingReservationAsync(HttpClient client, CancellationToken ct)
    {
        await AuthClient.SignInNewCustomerAsync(client);
        var spaceId = await NewSpaceAsync(ct);
        using (var first = await client.PostAsJsonAsync(Reservations, Body(spaceId, "2026-12-01T09:00:00Z", "2026-12-01T10:00:00Z", TestPlates.Next()), ct))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        return await client.PostAsJsonAsync(Reservations, Body(spaceId, "2026-12-01T09:00:00Z", "2026-12-01T10:00:00Z", TestPlates.Next()), ct);
    }

    // 10:07 local is not on the 15-minute grid.
    private async Task<HttpResponseMessage> OffGridReservationAsync(HttpClient client, CancellationToken ct)
    {
        await AuthClient.SignInNewCustomerAsync(client);
        var spaceId = await NewSpaceAsync(ct);

        return await client.PostAsJsonAsync(Reservations, Body(spaceId, "2026-12-01T09:07:00Z", "2026-12-01T10:07:00Z", "A12K345"), ct);
    }

    private static async Task<HttpResponseMessage> TooManyLoginsAsync(HttpClient client)
    {
        for (var attempt = 1; attempt <= AuthRateLimiting.PermitLimit; attempt++)
        {
            using var allowed = await AuthClient.LoginAsync(client, AuthClient.NewEmail(), AuthClient.Password);
        }

        return await AuthClient.LoginAsync(client, AuthClient.NewEmail(), AuthClient.Password);
    }

    private async Task<Guid> NewSpaceAsync(CancellationToken ct)
    {
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Contract"), spaces: 1, inactiveSpaces: 0, ct);
        return lot.Spaces.Single().Id;
    }

    private static object Body(Guid spaceId, string start, string end, string plate) =>
        new { spaceId, start, end, plate };

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

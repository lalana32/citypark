using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Auth;
using CityPark.Api.Tests.Lots;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

// Every request here is sent with CsrfHandler.Skip, so the test alone decides whether the header is there.
public class CsrfProtectionTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static readonly Uri Reservations = new("/api/v1/reservations", UriKind.Relative);

    [Fact]
    public async Task CsrfEndpoint_ReturnsTokenAndSetsHttpOnlyStrictCookie()
    {
        using var client = AuthClient.Create(factory, handleCookies: false);

        using var response = await client.GetAsync(AuthClient.Csrf, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await ReadTokenAsync(response)));
        var cookie = Assert.Single(
            SetCookieHeaderValue.ParseList([.. response.Headers.GetValues(HeaderNames.SetCookie)]),
            c => c.Name == CsrfProtection.CookieName);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }

    [Fact]
    public async Task CreateReservation_WithoutHeader_Returns400AndCreatesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);
        var spaceId = await NewSpaceAsync(ct);

        using var response = await PostAsync(client, Reservations, Reservation(spaceId), token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.GetProperty("type").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("general", out _));
        Assert.Equal(0, await OwnReservationCountAsync(client));
    }

    [Fact]
    public async Task CreateReservation_WithValidHeader_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);
        var spaceId = await NewSpaceAsync(ct);
        var token = await GetTokenAsync(client);

        using var response = await PostAsync(client, Reservations, Reservation(spaceId), token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await OwnReservationCountAsync(client));
    }

    // The token is bound to the user, so the SPA must fetch a new one after login.
    [Fact]
    public async Task CreateReservation_WithTokenFromBeforeLogin_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        var anonymousToken = await GetTokenAsync(client);
        await AuthClient.SignInNewCustomerAsync(client);
        var spaceId = await NewSpaceAsync(ct);

        using var response = await PostAsync(client, Reservations, Reservation(spaceId), anonymousToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await OwnReservationCountAsync(client));
    }

    // Login CSRF: another site must not be able to sign the browser in to the attacker's account.
    [Fact]
    public async Task Login_WithoutHeader_Returns400AndSetsNoSessionCookie()
    {
        using var client = AuthClient.Create(factory);
        var email = AuthClient.NewEmail();
        using (var registered = await AuthClient.RegisterAsync(client, email, AuthClient.Password))
        {
            registered.EnsureSuccessStatusCode();
        }

        using var response = await PostAsync(client, AuthClient.Login, new { email, password = AuthClient.Password }, token: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Uri uri, object body, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Options.Set(CsrfHandler.Skip, true);
        if (token is not null)
        {
            request.Headers.Add(CsrfProtection.HeaderName, token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> GetTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync(AuthClient.Csrf, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadTokenAsync(response);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("token").GetString()!;

    private static async Task<int> OwnReservationCountAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(Reservations, TestContext.Current.CancellationToken);
        return page.GetProperty("totalCount").GetInt32();
    }

    // 10:00–11:30 local on the fixed "now" of ClockedApiFactory.
    private static object Reservation(Guid spaceId) =>
        new { spaceId, start = "2026-12-01T09:00:00Z", end = "2026-12-01T10:30:00Z", plate = TestPlates.Next() };

    private async Task<Guid> NewSpaceAsync(CancellationToken ct)
    {
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Csrf"), spaces: 1, inactiveSpaces: 0, ct);
        return lot.Spaces.Single().Id;
    }
}

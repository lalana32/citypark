using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

public class AuthenticationEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string WrongPassword = "Not-The-Password-1";

    [Fact]
    public async Task Register_NewEmail_Returns201WithCustomerRole()
    {
        using var client = AuthClient.Create(factory);
        var email = AuthClient.NewEmail();

        using var response = await AuthClient.RegisterAsync(client, email, AuthClient.Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/auth/me", response.Headers.Location?.OriginalString);
        var user = await ReadJsonAsync(response);
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal(["Customer"], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    // Identity compares normalized (upper-case) emails, so another casing is the same account.
    [Fact]
    public async Task Register_TakenEmail_Returns409_AlsoInAnotherCase()
    {
        using var client = AuthClient.Create(factory);
        var email = AuthClient.NewEmail();
        using (var first = await AuthClient.RegisterAsync(client, email, AuthClient.Password))
        {
            first.EnsureSuccessStatusCode();
        }

        using var response = await AuthClient.RegisterAsync(client, email.ToUpperInvariant(), AuthClient.Password);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, (await ReadJsonAsync(response)).GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("Short-1")]
    [InlineData("no-digits-or-upper")]
    [InlineData("NoSymbols123")]
    public async Task Register_WeakPassword_Returns422WithPasswordErrors(string password)
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, AuthClient.NewEmail(), password);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors");
        var error = Assert.Single(errors.EnumerateObject());
        Assert.Equal("password", error.Name);
        Assert.NotEmpty(error.Value.EnumerateArray());
    }

    [Fact]
    public async Task Register_MalformedEmail_Returns400()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, "not-an-email", AuthClient.Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("email", Assert.Single((await ReadJsonAsync(response)).GetProperty("errors").EnumerateObject()).Name);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithUserAndSetsCookie()
    {
        using var client = AuthClient.Create(factory, handleCookies: false);
        var email = await RegisterAsync(client);

        using var response = await AuthClient.LoginAsync(client, email, AuthClient.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await ReadJsonAsync(response)).GetProperty("email").GetString());
        var cookie = AuthClient.SessionCookie(response);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401IdenticalToUnknownEmail()
    {
        using var client = AuthClient.Create(factory, handleCookies: false);
        var email = await RegisterAsync(client);

        using var wrongPassword = await AuthClient.LoginAsync(client, email, WrongPassword);
        using var unknownEmail = await AuthClient.LoginAsync(client, AuthClient.NewEmail(), WrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.False(wrongPassword.Headers.Contains(HeaderNames.SetCookie));
        Assert.Equal(Visible(await ReadJsonAsync(unknownEmail)), Visible(await ReadJsonAsync(wrongPassword)));
    }

    [Fact]
    public async Task Login_AfterFiveFailures_LocksAccountAndRejectsCorrectPassword()
    {
        using var client = AuthClient.Create(factory);
        var email = await RegisterAsync(client);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var failed = await AuthClient.LoginAsync(client, email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var response = await AuthClient.LoginAsync(client, email, AuthClient.Password);

        Assert.True(await factory.IsLockedOutAsync(email));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        using var client = AuthClient.Create(factory);

        using var response = await client.GetAsync(AuthClient.Me, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_WithCookie_ReturnsUserAndRoles()
    {
        using var client = AuthClient.Create(factory);
        var email = await AuthClient.SignInNewCustomerAsync(client);

        using var response = await client.GetAsync(AuthClient.Me, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await ReadJsonAsync(response);
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.NotEqual(Guid.Empty, user.GetProperty("id").GetGuid());
        Assert.Equal(["Customer"], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Logout_Returns204AndTheBrowserIsSignedOut()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using var logout = await client.PostAsync(AuthClient.Logout, content: null, ct);
        using var me = await client.GetAsync(AuthClient.Me, ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(AuthClient.SessionCookie(logout).Expires < DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    // A copy of the cookie taken before logout (for example a stolen one) must not work afterwards.
    [Fact]
    public async Task Logout_InvalidatesACopyOfTheOldCookie()
    {
        using var client = AuthClient.Create(factory, handleCookies: false);
        var email = await RegisterAsync(client);
        string cookie;
        using (var login = await AuthClient.LoginAsync(client, email, AuthClient.Password))
        {
            var session = AuthClient.SessionCookie(login);
            cookie = $"{session.Name}={session.Value}";
        }

        using var before = await SendWithCookieAsync(client, HttpMethod.Get, AuthClient.Me, cookie);
        using var logout = await SendWithCookieAsync(client, HttpMethod.Post, AuthClient.Logout, cookie);
        using var after = await SendWithCookieAsync(client, HttpMethod.Get, AuthClient.Me, cookie);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns401()
    {
        using var client = AuthClient.Create(factory);

        using var response = await client.PostAsync(AuthClient.Logout, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = AuthClient.NewEmail();
        using var response = await AuthClient.RegisterAsync(client, email, AuthClient.Password);
        response.EnsureSuccessStatusCode();
        return email;
    }

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, HttpMethod method, Uri uri, string cookie)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(HeaderNames.Cookie, cookie);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    // Everything a client can see, except the per-request instance and traceId.
    private static (int?, string?, string?, string?) Visible(JsonElement problem) => (
        problem.GetProperty("status").GetInt32(),
        problem.GetProperty("type").GetString(),
        problem.GetProperty("title").GetString(),
        problem.GetProperty("detail").GetString());
}

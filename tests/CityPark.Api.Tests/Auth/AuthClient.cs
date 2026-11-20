using System.Net.Http.Json;

using CityPark.Api.Auth;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

/// <summary>Cookie-aware clients and calls to the real auth endpoints.</summary>
internal static class AuthClient
{
    public static readonly Uri Register = new("/api/v1/auth/register", UriKind.Relative);
    public static readonly Uri Login = new("/api/v1/auth/login", UriKind.Relative);
    public static readonly Uri Logout = new("/api/v1/auth/logout", UriKind.Relative);
    public static readonly Uri Me = new("/api/v1/auth/me", UriKind.Relative);

    /// <summary>Strong enough for the password rules in AuthSetup.</summary>
    public const string Password = "Parking-Spot-42";

    /// <summary>A new address per test, so tests sharing one database never see each other's users.</summary>
    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    // HTTPS base address: outside Development the cookie is Secure, and the client keeps it only for HTTPS.
    public static HttpClient Create(WebApplicationFactory<Program> factory, bool handleCookies = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies,
        });

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(Register, new { email, password }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(Login, new { email, password }, TestContext.Current.CancellationToken);

    /// <summary>Registers a new customer and signs the client in.</summary>
    public static async Task<string> SignInNewCustomerAsync(HttpClient client)
    {
        var email = NewEmail();

        using (var registered = await RegisterAsync(client, email, Password))
        {
            registered.EnsureSuccessStatusCode();
        }

        await LoginExpectingSuccessAsync(client, email);
        return email;
    }

    public static async Task LoginExpectingSuccessAsync(HttpClient client, string email)
    {
        using var response = await LoginAsync(client, email, Password);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The session cookie a response sets.</summary>
    public static SetCookieHeaderValue SessionCookie(HttpResponseMessage response) =>
        Assert.Single(
            SetCookieHeaderValue.ParseList([.. response.Headers.GetValues(HeaderNames.SetCookie)]),
            cookie => cookie.Name == AuthSetup.CookieName);
}

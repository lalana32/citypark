using System.Net;

using CityPark.Api.Auth;
using CityPark.Application.Common;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

public class CookieAuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ProtectedEndpoint_WithoutCookie_Returns401InsteadOfRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using var response = await client.GetAsync(Relative(AuthEndpoints.SignedInOnly), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithCookie_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        await SignInAsync(client, AppRoles.Customer, ct);

        using var response = await client.GetAsync(Relative(AuthEndpoints.SignedInOnly), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsCustomer_Returns403InsteadOfRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        await SignInAsync(client, AppRoles.Customer, ct);

        using var response = await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AdminEndpoint_AsAdmin_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        await SignInAsync(client, AppRoles.Admin, ct);

        using var response = await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The factory runs in Production, so the cookie must carry Secure as well.
    [Fact]
    public async Task SessionCookie_IsHttpOnlySecureAndSameSiteStrict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient(handleCookies: false);

        using var response = await PostSignInAsync(client, AppRoles.Customer, ct);

        var cookie = Assert.Single(
            SetCookieHeaderValue.ParseList([.. response.Headers.GetValues(HeaderNames.SetCookie)]),
            c => c.Name == AuthSetup.CookieName);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }

    // HTTPS base address: the client keeps a Secure cookie only for HTTPS requests.
    private HttpClient CreateClient(bool handleCookies = true) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = handleCookies,
    });

    private static async Task SignInAsync(HttpClient client, string role, CancellationToken ct)
    {
        using var response = await PostSignInAsync(client, role, ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostSignInAsync(HttpClient client, string role, CancellationToken ct) =>
        client.PostAsync(Relative($"{AuthEndpoints.SignIn}?role={role}"), content: null, ct);

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

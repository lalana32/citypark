using System.Net;

using CityPark.Application.Common;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

public class CookieAuthenticationTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task ProtectedEndpoint_WithoutCookie_Returns401InsteadOfRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);

        using var response = await client.GetAsync(Relative(AuthEndpoints.SignedInOnly), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithCookie_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using var response = await client.GetAsync(Relative(AuthEndpoints.SignedInOnly), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsCustomer_Returns403InsteadOfRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using var response = await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AdminEndpoint_AsAdmin_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        var email = AuthClient.NewEmail();
        await factory.CreateUserAsync(email, AuthClient.Password, AppRoles.Admin);
        await AuthClient.LoginExpectingSuccessAsync(client, email);

        using var response = await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The factory runs in Production, so the cookie must carry Secure as well.
    [Fact]
    public async Task SessionCookie_IsHttpOnlySecureAndSameSiteStrict()
    {
        using var client = AuthClient.Create(factory, handleCookies: false);
        var email = AuthClient.NewEmail();
        using (var registered = await AuthClient.RegisterAsync(client, email, AuthClient.Password))
        {
            registered.EnsureSuccessStatusCode();
        }

        using var response = await AuthClient.LoginAsync(client, email, AuthClient.Password);

        var cookie = AuthClient.SessionCookie(response);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

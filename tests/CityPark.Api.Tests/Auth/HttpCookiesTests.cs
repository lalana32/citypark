using System.Net;

using CityPark.Api.Auth;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

public class HttpCookiesTests(HttpCookiesApiFactory factory) : IClassFixture<HttpCookiesApiFactory>
{
    [Fact]
    public async Task CsrfEndpoint_OverHttpWithAllowHttp_Returns200AndCookieWithoutSecure()
    {
        using var client = factory.CreateDefaultClient(new Uri("http://localhost"));

        using var response = await client.GetAsync(AuthClient.Csrf, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(
            SetCookieHeaderValue.ParseList([.. response.Headers.GetValues(HeaderNames.SetCookie)]),
            c => c.Name == CsrfProtection.CookieName);
        Assert.False(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
    }
}

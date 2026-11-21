using System.Net;

using Microsoft.Net.Http.Headers;

namespace CityPark.Api.Tests.Auth;

// Same-origin deployment: CORS is not enabled, so a browser on another site cannot read any response.
public class CorsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string OtherSite = "https://evil.example";

    [Fact]
    public async Task GetFromAnotherOrigin_HasNoAllowOriginHeader()
    {
        using var client = AuthClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/lots", UriKind.Relative));
        request.Headers.Add(HeaderNames.Origin, OtherSite);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderNames.AccessControlAllowOrigin));
    }

    [Fact]
    public async Task PreflightFromAnotherOrigin_HasNoAllowOriginHeader()
    {
        using var client = AuthClient.Create(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v1/reservations", UriKind.Relative));
        request.Headers.Add(HeaderNames.Origin, OtherSite);
        request.Headers.Add(HeaderNames.AccessControlRequestMethod, "POST");
        request.Headers.Add(HeaderNames.AccessControlRequestHeaders, "x-xsrf-token, content-type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains(HeaderNames.AccessControlAllowOrigin));
        Assert.False(response.Headers.Contains(HeaderNames.AccessControlAllowHeaders));
    }
}

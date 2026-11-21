using System.Net;
using System.Net.Http.Json;

using CityPark.Api.Tests.Auth;

namespace CityPark.Api.Tests.Authorization;

// The permission matrix through the full HTTP pipeline (docs/testing.md).
public class AuthorizationMatrixTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private const string AnyId = "0193a4b2-0000-7000-8000-000000000001";

    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "POST", "/api/v1/auth/logout" },
        { "GET", "/api/v1/auth/me" },
        { "POST", "/api/v1/reservations" },
        { "GET", "/api/v1/reservations" },
        { "GET", $"/api/v1/reservations/{AnyId}" },
        { "POST", $"/api/v1/reservations/{AnyId}/cancel" },
        { "GET", AuthEndpoints.AdminOnly },
    };

    public static TheoryData<string, HttpStatusCode> PublicEndpoints => new()
    {
        { "/api/v1/lots", HttpStatusCode.OK },
        { $"/api/v1/lots/{AnyId}", HttpStatusCode.NotFound },
        { $"/api/v1/lots/{AnyId}/availability?start=2026-12-01T09:00:00Z&end=2026-12-01T10:00:00Z", HttpStatusCode.NotFound },
        { "/api/v1/auth/csrf", HttpStatusCode.OK },
        { "/health/live", HttpStatusCode.OK },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_Anonymous_Returns401Problem(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);
        using var request = Request(method, path);

        using var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [MemberData(nameof(PublicEndpoints))]
    public async Task PublicEndpoint_Anonymous_IsNotRejected(string path, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);

        Assert.Equal(expected, response.StatusCode);
    }

    // POST carries a JSON body like a real client: the 401 must come before the body is validated.
    private static HttpRequestMessage Request(string method, string path) =>
        new(new HttpMethod(method), new Uri(path, UriKind.Relative))
        {
            Content = method == "POST" ? JsonContent.Create(new { }) : null,
        };
}

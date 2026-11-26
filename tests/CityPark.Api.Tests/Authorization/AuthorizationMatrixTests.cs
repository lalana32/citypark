using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;
using CityPark.Application.Common;

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
        { "POST", "/api/v1/admin/lots" },
        { "PUT", $"/api/v1/admin/lots/{AnyId}" },
        { "POST", $"/api/v1/admin/lots/{AnyId}/spaces" },
        { "POST", $"/api/v1/admin/lots/{AnyId}/spaces/{AnyId}/deactivate" },
        { "GET", "/api/v1/admin/reservations" },
        { "POST", $"/api/v1/admin/reservations/{AnyId}/cancel" },
    };

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", AuthEndpoints.AdminOnly },
        { "POST", "/api/v1/admin/lots" },
        { "PUT", $"/api/v1/admin/lots/{AnyId}" },
        { "POST", $"/api/v1/admin/lots/{AnyId}/spaces" },
        { "POST", $"/api/v1/admin/lots/{AnyId}/spaces/{AnyId}/deactivate" },
        { "GET", "/api/v1/admin/reservations" },
        { "POST", $"/api/v1/admin/reservations/{AnyId}/cancel" },
    };

    public static TheoryData<string, HttpStatusCode> PublicEndpoints => new()
    {
        { "/api/v1/lots", HttpStatusCode.OK },
        { $"/api/v1/lots/{AnyId}", HttpStatusCode.NotFound },
        { $"/api/v1/lots/{AnyId}/availability?start=2026-12-01T09:00:00Z&end=2026-12-01T10:00:00Z", HttpStatusCode.NotFound },
        { $"/api/v1/lots/{AnyId}/quote?start=2026-12-01T09:00:00Z&end=2026-12-01T10:00:00Z", HttpStatusCode.NotFound },
        { "/api/v1/auth/csrf", HttpStatusCode.OK },
        { "/health/live", HttpStatusCode.OK },
    };

    public static TheoryData<string> SignedInRoles => new() { AppRoles.Customer, AppRoles.Admin };

    // GET or cancel a reservation of another customer, as a customer or an admin.
    public static TheoryData<string, string> AnotherUsersReservation => new()
    {
        { AppRoles.Customer, "GET" },
        { AppRoles.Customer, "POST" },
        { AppRoles.Admin, "GET" },
        { AppRoles.Admin, "POST" },
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

        using var response = await client.GetAsync(Relative(path), ct);

        Assert.Equal(expected, response.StatusCode);
    }

    // The empty body is invalid for most of these routes: the 403 must come before validation.
    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AdminEndpoint_Customer_Returns403Problem(string method, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await SignedInAsync(AppRoles.Customer);
        using var request = Request(method, path);

        using var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AdminEndpoint_Admin_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await SignedInAsync(AppRoles.Admin);

        using var response = await client.GetAsync(Relative(AuthEndpoints.AdminOnly), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(SignedInRoles))]
    public async Task OwnEndpoints_SignedIn_Succeed(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await SignedInAsync(role);
        var id = await CreateReservationAsync(client, ct);

        using var me = await client.GetAsync(Relative("/api/v1/auth/me"), ct);
        using var list = await client.GetAsync(Relative("/api/v1/reservations"), ct);
        using var get = await client.GetAsync(Relative($"/api/v1/reservations/{id}"), ct);
        using var cancel = await client.PostAsync(Relative($"/api/v1/reservations/{id}/cancel"), content: null, ct);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
    }

    // Not 403: a 403 would confirm that the id exists.
    [Theory]
    [MemberData(nameof(AnotherUsersReservation))]
    public async Task AnotherUsersReservation_Returns404Problem(string role, string method)
    {
        var ct = TestContext.Current.CancellationToken;
        using var owner = await SignedInAsync(AppRoles.Customer);
        var id = await CreateReservationAsync(owner, ct);
        using var caller = await SignedInAsync(role);
        using var request = Request(method, method == "POST" ? $"/api/v1/reservations/{id}/cancel" : $"/api/v1/reservations/{id}");

        using var response = await caller.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var state = (await owner.GetFromJsonAsync<JsonElement>(Relative($"/api/v1/reservations/{id}"), ct)).GetProperty("state");
        Assert.Equal("Upcoming", state.GetString());
    }

    private async Task<HttpClient> SignedInAsync(string role)
    {
        var client = AuthClient.Create(factory);

        if (role == AppRoles.Admin)
        {
            await AuthClient.SignInNewAdminAsync(factory, client);
        }
        else
        {
            await AuthClient.SignInNewCustomerAsync(client);
        }

        return client;
    }

    // 10:00–11:30 local on a new lot's only space; "now" is 08:00 (ClockedApiFactory).
    private async Task<Guid> CreateReservationAsync(HttpClient client, CancellationToken ct)
    {
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Matrix"), spaces: 1, inactiveSpaces: 0, ct);
        var body = new { spaceId = lot.Spaces.Single().Id, start = "2026-12-01T09:00:00Z", end = "2026-12-01T10:30:00Z", plate = TestPlates.Next() };

        using var response = await client.PostAsJsonAsync(Relative("/api/v1/reservations"), body, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
    }

    // POST and PUT carry a JSON body like a real client: 401 and 403 must come before the body is validated.
    private static HttpRequestMessage Request(string method, string path) =>
        new(new HttpMethod(method), Relative(path))
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
        };

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Reservations;
using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;

namespace CityPark.Api.Tests.Reservations;

// "Now" is 08:00 local on Tuesday 1 December 2026 (ClockedApiFactory); lots open 07:00–22:00 at 2.50 BAM.
// Each test adds its own lot and signs in new users, so tests sharing the database never collide.
public class ReservationEndpointsTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static readonly Uri Reservations = new("/api/v1/reservations", UriKind.Relative);

    // 10:00–11:30 local: 90 minutes, billed as 2 hours (R8).
    private const string Start = "2026-12-01T09:00:00Z";
    private const string End = "2026-12-01T10:30:00Z";

    [Fact]
    public async Task Create_WithoutCookie_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);

        using var response = await client.PostAsJsonAsync(
            Reservations, Body(Guid.CreateVersion7(), Start, End, "A12K345"), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithLocationAndPrice()
    {
        var ct = TestContext.Current.CancellationToken;
        var spaceId = await NewSpaceAsync(ct);
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using var response = await client.PostAsJsonAsync(Reservations, Body(spaceId, Start, End, "a12-k-345"), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/reservations/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(5.00m, body.GetProperty("price").GetDecimal());
        Assert.Equal("BAM", body.GetProperty("currency").GetString());
        Assert.Equal("A12K345", body.GetProperty("plate").GetString());
        Assert.Equal("A-01", body.GetProperty("spaceLabel").GetString());
        Assert.Equal("Upcoming", body.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Create_OverlappingIntervalOnTheSameSpace_Returns409Problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var spaceId = await NewSpaceAsync(ct);
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using (var first = await client.PostAsJsonAsync(Reservations, Body(spaceId, Start, End, "A12K345"), ct))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        // 10:30–11:30 local overlaps 10:00–11:30; another plate, so only the space rule (R6) can fail.
        using var second = await client.PostAsJsonAsync(
            Reservations, Body(spaceId, "2026-12-01T09:30:00Z", "2026-12-01T10:30:00Z", "E12K345"), ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.10", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Get_OwnReservation_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var owner = await SignedInClientAsync();
        var id = await CreateAsync(owner, await NewSpaceAsync(ct), "A12K345", ct);

        using var response = await owner.GetAsync(ReservationEndpoints.Location(id), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_AnotherUsersReservation_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var userA = await SignedInClientAsync();
        using var userB = await SignedInClientAsync();
        var id = await CreateAsync(userA, await NewSpaceAsync(ct), "A12K345", ct);

        using var response = await userB.GetAsync(ReservationEndpoints.Location(id), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnItemsWithPageMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        using var userA = await SignedInClientAsync();
        using var userB = await SignedInClientAsync();
        HashSet<Guid> own =
        [
            await CreateAsync(userA, await NewSpaceAsync(ct), "A12K345", ct),
            await CreateAsync(userA, await NewSpaceAsync(ct), "E12K345", ct),
        ];
        await CreateAsync(userB, await NewSpaceAsync(ct), "J12K345", ct);

        var first = await userA.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/reservations?page=1&pageSize=1", UriKind.Relative), ct);
        var second = await userA.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/reservations?page=2&pageSize=1", UriKind.Relative), ct);

        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(1, first.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());

        // Both pages together are exactly user A's reservations; user B's is on neither.
        var listed = new[] { first, second }
            .SelectMany(p => p.GetProperty("items").EnumerateArray())
            .Select(i => i.GetProperty("id").GetGuid());
        Assert.Equal(own.Order(), listed.Order());
    }

    [Fact]
    public async Task List_WithoutCookie_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = AuthClient.Create(factory);

        using var response = await client.GetAsync(Reservations, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);
        return client;
    }

    // Books 10:00–11:30 local on the given space and returns the new id.
    private static async Task<Guid> CreateAsync(HttpClient client, Guid spaceId, string plate, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(Reservations, Body(spaceId, Start, End, plate), ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
    }

    private static object Body(Guid spaceId, string start, string end, string plate) =>
        new { spaceId, start, end, plate };

    private async Task<Guid> NewSpaceAsync(CancellationToken ct)
    {
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), spaces: 1, inactiveSpaces: 0, ct);
        return lot.Spaces.Single().Id;
    }
}

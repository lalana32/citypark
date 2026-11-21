using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    private static object Body(Guid spaceId, string start, string end, string plate) =>
        new { spaceId, start, end, plate };

    private async Task<Guid> NewSpaceAsync(CancellationToken ct)
    {
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), spaces: 1, inactiveSpaces: 0, ct);
        return lot.Spaces.Single().Id;
    }
}

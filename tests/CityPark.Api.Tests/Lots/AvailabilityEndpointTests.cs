using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CityPark.Api.Tests.Lots;

// "Now" is 08:00 local on Tuesday 1 December 2026 (ClockedApiFactory); the lot opens 07:00–22:00.
// Which spaces are free under reservations is covered against PostgreSQL in AvailabilityServiceTests.
public class AvailabilityEndpointTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    // 10:00–11:00 local, once in UTC and once with the local offset. "+" must be sent as %2B in a query.
    [Theory]
    [InlineData("2026-12-01T09:00:00Z", "2026-12-01T10:00:00Z")]
    [InlineData("2026-12-01T10:00:00%2B01:00", "2026-12-01T11:00:00%2B01:00")]
    public async Task ValidInterval_Returns200WithCountsAndFreeSpaces(string start, string end)
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), spaces: 3, inactiveSpaces: 1, ct);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Availability(lot.Id, $"start={start}&end={end}"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(lot.Id, body.GetProperty("lotId").GetGuid());
        Assert.Equal("2026-12-01T10:00:00+01:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-12-01T11:00:00+01:00", body.GetProperty("end").GetString());
        Assert.Equal(2, body.GetProperty("totalSpaces").GetInt32());
        Assert.Equal(2, body.GetProperty("availableSpaces").GetInt32());
        Assert.Equal(
            ["A-02", "A-03"],
            body.GetProperty("freeSpaces").EnumerateArray().Select(s => s.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task NonAlignedInterval_Returns422ForStart()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), spaces: 1, inactiveSpaces: 0, ct);
        using var client = factory.CreateClient();

        // 09:10Z is 10:10 local, off the 15-minute grid (R1).
        using var response = await client.GetAsync(
            Availability(lot.Id, "start=2026-12-01T09:10:00Z&end=2026-12-01T10:00:00Z"), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors");
        Assert.Equal("start", Assert.Single(errors.EnumerateObject()).Name);
    }

    [Theory]
    [InlineData("end=2026-12-01T10:00:00Z", "start")]
    [InlineData("start=2026-12-01T09:00:00Z", "end")]
    public async Task MissingParameter_Returns400ForThatParameter(string query, string missing)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        // No lot needed: the shape is checked before the handler reads the database.
        using var response = await client.GetAsync(Availability(Guid.CreateVersion7(), query), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors");
        Assert.Equal(missing, Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task UnknownLot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            Availability(Guid.CreateVersion7(), "start=2026-12-01T09:00:00Z&end=2026-12-01T10:00:00Z"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Uri Availability(Guid lotId, string query) =>
        new($"/api/v1/lots/{lotId}/availability?{query}", UriKind.Relative);
}

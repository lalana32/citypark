using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Tests.Auth;
using CityPark.Domain.Lots;

namespace CityPark.Api.Tests.Lots;

// "Now" is 08:00 local on Tuesday 1 December 2026 (ClockedApiFactory); the lot opens 07:00–22:00.
public class QuoteEndpointTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static readonly Rate TwoPerHour = new(2.00m, "BAM");

    // 10:00–11:15 local: 75 minutes, billed as 2 hours (R8).
    private const string Start = "2026-12-01T09:00:00Z";
    private const string End = "2026-12-01T10:15:00Z";

    [Fact]
    public async Task StartedHour_IsBilledAsAFullHour()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), 1, 0, TwoPerHour, ct);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Quote(lot.Id, $"start={Start}&end={End}"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(lot.Id, body.GetProperty("lotId").GetGuid());
        Assert.Equal("2026-12-01T10:00:00+01:00", body.GetProperty("start").GetString());
        Assert.Equal("2026-12-01T11:15:00+01:00", body.GetProperty("end").GetString());
        Assert.Equal(2, body.GetProperty("billedHours").GetInt32());
        Assert.Equal(4.00m, body.GetProperty("price").GetDecimal());
        Assert.Equal("BAM", body.GetProperty("currency").GetString());
    }

    [Theory]
    [InlineData("2026-12-01T09:10:00Z", "2026-12-01T10:00:00Z")] // start off the 15-minute grid (R1)
    [InlineData("2026-12-01T10:00:00Z", "2026-12-01T09:00:00Z")] // end before start
    [InlineData("2026-12-01T09:00:00Z", "2026-12-01T09:15:00Z")] // shorter than 30 minutes
    [InlineData("2026-12-01T20:00:00Z", "2026-12-01T22:00:00Z")] // past closing time
    public async Task InvalidInterval_Returns422(string start, string end)
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), 1, 0, TwoPerHour, ct);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Quote(lot.Id, $"start={start}&end={end}"), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("end=2026-12-01T10:00:00Z", "start")]
    [InlineData("start=2026-12-01T09:00:00Z", "end")]
    public async Task MissingParameter_Returns400ForThatParameter(string query, string missing)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Quote(Guid.CreateVersion7(), query), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("errors");
        Assert.Equal(missing, Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task UnknownLot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Quote(Guid.CreateVersion7(), $"start={Start}&end={End}"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quote_EqualsThePriceOfAReservationMadeAfterwards()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), 1, 0, TwoPerHour, ct);
        using var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);

        using var quoteResponse = await client.GetAsync(Quote(lot.Id, $"start={Start}&end={End}"), ct);
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = await quoteResponse.Content.ReadFromJsonAsync<JsonElement>(ct);

        using var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/reservations", UriKind.Relative),
            new { lotId = lot.Id, start = Start, end = End, plate = "A12K345" },
            ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var reservation = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(quote.GetProperty("price").GetDecimal(), reservation.GetProperty("price").GetDecimal());
        Assert.Equal(quote.GetProperty("currency").GetString(), reservation.GetProperty("currency").GetString());
    }

    private static Uri Quote(Guid lotId, string query) =>
        new($"/api/v1/lots/{lotId}/quote?{query}", UriKind.Relative);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CityPark.Api.Tests.Lots;

public class LotEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task List_Returns200WithSeededLots()
    {
        var ct = TestContext.Current.CancellationToken;
        var centar = await TestLots.AddAsync(factory, TestLots.UniqueName("Centar"), spaces: 3, inactiveSpaces: 1, ct);
        var marijinDvor = await TestLots.AddAsync(factory, TestLots.UniqueName("Marijin Dvor"), spaces: 2, inactiveSpaces: 0, ct);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/lots?pageSize=50", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(50, body.GetProperty("pageSize").GetInt32());

        var items = body.GetProperty("items").EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(centar.Id, ids);
        Assert.Contains(marijinDvor.Id, ids);
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);

        // The inactive space is not capacity.
        var item = items.Single(i => i.GetProperty("id").GetGuid() == centar.Id);
        Assert.Equal(2, item.GetProperty("totalSpaces").GetInt32());
    }

    [Fact]
    public async Task List_PageZero_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/lots?page=0", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_KnownLot_Returns200WithItsFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Baščaršija"), spaces: 4, inactiveSpaces: 0, ct);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/v1/lots/{lot.Id}", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(lot.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(lot.Name, body.GetProperty("name").GetString());
        Assert.Equal("Ferhadija 1", body.GetProperty("address").GetString());
        Assert.Equal(2.50m, body.GetProperty("hourlyPrice").GetDecimal());
        Assert.Equal("BAM", body.GetProperty("currency").GetString());
        Assert.Equal("07:00:00", body.GetProperty("opensAt").GetString());
        Assert.Equal("22:00:00", body.GetProperty("closesAt").GetString());
        Assert.Equal(4, body.GetProperty("totalSpaces").GetInt32());
    }

    [Fact]
    public async Task Get_UnknownLot_Returns404ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/v1/lots/{Guid.CreateVersion7()}", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("status").GetInt32());
    }
}

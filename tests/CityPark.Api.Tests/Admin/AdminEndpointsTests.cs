using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;

namespace CityPark.Api.Tests.Admin;

// "Now" is 08:00 local on Tuesday 1 December 2026 (ClockedApiFactory). TestLots open 07:00–22:00 at 2.50 BAM
// with spaces A-01, A-02, ...; each test adds its own lot. 403 for a customer is in AuthorizationMatrixTests.
public class AdminEndpointsTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static readonly Uri AdminLots = new("/api/v1/admin/lots", UriKind.Relative);

    [Fact]
    public async Task CreateLot_ValidBody_Returns201WithLocationOfThePublicLot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await SignedInAdminAsync();
        var name = TestLots.UniqueName("Admin");

        using var response = await admin.PostAsJsonAsync(AdminLots, LotBody(name), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/lots/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(name, created.GetProperty("name").GetString());
        Assert.Equal(0, created.GetProperty("totalSpaces").GetInt32());
    }

    [Fact]
    public async Task CreateLot_ClosingBeforeOpening_Returns422ForClosesAt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PostAsJsonAsync(
            AdminLots, LotBody(TestLots.UniqueName("Invalid"), opensAt: "22:00:00", closesAt: "07:00:00"), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("closesAt", out _));
    }

    [Fact]
    public async Task UpdateLot_NewRate_DoesNotChangeThePriceOfAnExistingReservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Rate"), spaces: 1, inactiveSpaces: 0, ct);
        using var customer = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(customer);
        var reservationId = await ReserveTenToHalfPastElevenAsync(customer, lot.Spaces.Single().Id, ct);
        using var admin = await SignedInAdminAsync();

        using var update = await admin.PutAsJsonAsync(LotRoute(lot.Id), LotBody(lot.Name, hourlyPrice: 4.00m), ct);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(4.00m, (await update.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("hourlyPrice").GetDecimal());
        var reservation = await customer.GetFromJsonAsync<JsonElement>(Relative($"/api/v1/reservations/{reservationId}"), ct);
        Assert.Equal(5.00m, reservation.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task UpdateLot_UnknownLot_Returns404Problem()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PutAsJsonAsync(LotRoute(Guid.CreateVersion7()), LotBody("Nowhere"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddSpace_NewLabel_Returns201WithTheNormalizedActiveSpace()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Space"), spaces: 1, inactiveSpaces: 0, ct);
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PostAsJsonAsync(SpacesRoute(lot.Id), new { label = " b-01 " }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var space = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("B-01", space.GetProperty("label").GetString());
        Assert.True(space.GetProperty("isActive").GetBoolean());
        Assert.Equal(2, await TotalSpacesAsync(admin, lot.Id, ct));
    }

    [Fact]
    public async Task AddSpace_DuplicateLabelInTheLot_Returns409Problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Duplicate"), spaces: 1, inactiveSpaces: 0, ct);
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PostAsJsonAsync(SpacesRoute(lot.Id), new { label = "a-01" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, await TotalSpacesAsync(admin, lot.Id, ct));
    }

    [Fact]
    public async Task DeactivateSpace_WithAFutureActiveReservation_Returns409ListingItAndKeepsTheSpaceActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Busy"), spaces: 1, inactiveSpaces: 0, ct);
        var spaceId = lot.Spaces.Single().Id;
        using var customer = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(customer);
        var reservationId = await ReserveTenToHalfPastElevenAsync(customer, spaceId, ct);
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PostAsync(DeactivateRoute(lot.Id, spaceId), content: null, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(reservationId, Assert.Single(problem.GetProperty("reservationIds").EnumerateArray()).GetGuid());
        Assert.Equal(1, await TotalSpacesAsync(admin, lot.Id, ct));
    }

    [Fact]
    public async Task DeactivateSpace_WithoutReservations_Returns204AndTheSpaceIsNoLongerCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Empty"), spaces: 2, inactiveSpaces: 0, ct);
        using var admin = await SignedInAdminAsync();

        using var response = await admin.PostAsync(DeactivateRoute(lot.Id, lot.Spaces.First().Id), content: null, ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await TotalSpacesAsync(admin, lot.Id, ct));
    }

    private async Task<HttpClient> SignedInAdminAsync()
    {
        var client = AuthClient.Create(factory);
        await AuthClient.SignInNewAdminAsync(factory, client);
        return client;
    }

    // 10:00–11:30 local at 2.50 BAM: 90 minutes, billed as 2 hours, so 5.00 BAM (R8).
    private static async Task<Guid> ReserveTenToHalfPastElevenAsync(HttpClient customer, Guid spaceId, CancellationToken ct)
    {
        var body = new { spaceId, start = "2026-12-01T09:00:00Z", end = "2026-12-01T10:30:00Z", plate = "A12K345" };

        using var response = await customer.PostAsJsonAsync(Relative("/api/v1/reservations"), body, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(5.00m, reservation.GetProperty("price").GetDecimal());
        return reservation.GetProperty("id").GetGuid();
    }

    // The public lot counts active spaces only.
    private static async Task<int> TotalSpacesAsync(HttpClient client, Guid lotId, CancellationToken ct) =>
        (await client.GetFromJsonAsync<JsonElement>(Relative($"/api/v1/lots/{lotId}"), ct)).GetProperty("totalSpaces").GetInt32();

    private static object LotBody(
        string name,
        decimal hourlyPrice = 2.50m,
        string opensAt = "07:00:00",
        string closesAt = "22:00:00") =>
        new { name, address = "Ferhadija 1", hourlyPrice, currency = "BAM", opensAt, closesAt };

    private static Uri LotRoute(Guid lotId) => Relative($"/api/v1/admin/lots/{lotId}");

    private static Uri SpacesRoute(Guid lotId) => Relative($"/api/v1/admin/lots/{lotId}/spaces");

    private static Uri DeactivateRoute(Guid lotId, Guid spaceId) =>
        Relative($"/api/v1/admin/lots/{lotId}/spaces/{spaceId}/deactivate");

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

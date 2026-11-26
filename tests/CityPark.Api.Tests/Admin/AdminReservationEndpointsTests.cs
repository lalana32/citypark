using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;
using CityPark.Domain.Lots;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Api.Tests.Admin;

// A class of its own, so moving the shared fake clock cannot affect other tests.
// 403 for a customer is in AuthorizationMatrixTests.
public class AdminReservationEndpointsTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private static int _dayNumber;

    private FakeTimeProvider Clock => (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

    [Fact]
    public async Task List_ByLot_ReturnsTheReservationsOfEveryCustomer()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = StartNewDay();
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("All"), spaces: 2, inactiveSpaces: 0, ct);
        using var customerA = await SignedInCustomerAsync();
        using var customerB = await SignedInCustomerAsync();
        var a = await ReserveAsync(customerA, lot.Spaces.First(), day.AddHours(2), ct);
        var b = await ReserveAsync(customerB, lot.Spaces.Last(), day.AddHours(2), ct);
        using var admin = await SignedInAdminAsync();

        var page = await admin.GetFromJsonAsync<JsonElement>(Relative($"/api/v1/admin/reservations?lotId={lot.Id}"), ct);

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { a, b }.Order(), items.Select(i => i.GetProperty("id").GetGuid()).Order());
        Assert.Equal(2, items.Select(i => i.GetProperty("userId").GetGuid()).Distinct().Count());
        Assert.All(items, i => Assert.Equal("Active", i.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task List_StatusCancelled_ReturnsOnlyCancelledReservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = StartNewDay();
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Status"), spaces: 1, inactiveSpaces: 0, ct);
        using var customer = await SignedInCustomerAsync();
        var cancelled = await ReserveAsync(customer, lot.Spaces.First(), day.AddHours(2), ct);
        await ReserveAsync(customer, lot.Spaces.First(), day.AddHours(4), ct);
        using (var cancel = await customer.PostAsync(Relative($"/api/v1/reservations/{cancelled}/cancel"), content: null, ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        }

        using var admin = await SignedInAdminAsync();

        var page = await admin.GetFromJsonAsync<JsonElement>(
            Relative($"/api/v1/admin/reservations?lotId={lot.Id}&status=Cancelled"), ct);

        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(cancelled, item.GetProperty("id").GetGuid());
        Assert.Equal("Cancelled", item.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("status=Expired")]
    [InlineData("status=7")]
    [InlineData("from=2026-12-02&to=2026-12-01")]
    public async Task List_InvalidFilter_Returns400Problem(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await SignedInAdminAsync();

        using var response = await admin.GetAsync(Relative($"/api/v1/admin/reservations?{query}"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Cancel_ReservationStartingInOneMinute_Returns200WithStatusCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = StartNewDay();
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Soon"), spaces: 1, inactiveSpaces: 0, ct);
        using var customer = await SignedInCustomerAsync();
        var id = await ReserveAsync(customer, lot.Spaces.First(), day.AddMinutes(15), ct);
        using var admin = await SignedInAdminAsync();

        // 08:14 local, one minute before the 08:15 start.
        Clock.SetUtcNow(day.AddMinutes(14));
        using var response = await admin.PostAsync(Relative($"/api/v1/admin/reservations/{id}/cancel"), content: null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.Equal("Cancelled", await CustomerStateAsync(customer, id, ct));
    }

    [Fact]
    public async Task Cancel_ReservationInProgress_Returns200_WhereACustomerGets422()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = StartNewDay();
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Started"), spaces: 1, inactiveSpaces: 0, ct);
        using var customer = await SignedInCustomerAsync();
        var id = await ReserveAsync(customer, lot.Spaces.First(), day.AddHours(2), ct);
        using var admin = await SignedInAdminAsync();

        // 10:30 local, half an hour into the 10:00–11:00 reservation.
        Clock.SetUtcNow(day.AddHours(2.5));
        using var byCustomer = await customer.PostAsync(Relative($"/api/v1/reservations/{id}/cancel"), content: null, ct);
        using var byAdmin = await admin.PostAsync(Relative($"/api/v1/admin/reservations/{id}/cancel"), content: null, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, byCustomer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal("Cancelled", (await byAdmin.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("state").GetString());
    }

    [Fact]
    public async Task Cancel_Twice_Returns409_AndUnknownId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var day = StartNewDay();
        var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Twice"), spaces: 1, inactiveSpaces: 0, ct);
        using var customer = await SignedInCustomerAsync();
        var id = await ReserveAsync(customer, lot.Spaces.First(), day.AddHours(2), ct);
        using var admin = await SignedInAdminAsync();
        using (var first = await admin.PostAsync(Relative($"/api/v1/admin/reservations/{id}/cancel"), content: null, ct))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await admin.PostAsync(Relative($"/api/v1/admin/reservations/{id}/cancel"), content: null, ct);
        using var unknown = await admin.PostAsync(
            Relative($"/api/v1/admin/reservations/{Guid.CreateVersion7()}/cancel"), content: null, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // FakeTimeProvider cannot go back, and tests of a class run one after another, so each test
    // moves the clock forward to 08:00 local on a day of its own and books on that day.
    private DateTimeOffset StartNewDay()
    {
        var day = ClockedApiFactory.Now.AddDays(Interlocked.Increment(ref _dayNumber));
        Clock.SetUtcNow(day);
        return day;
    }

    // One hour from the given start, booked through the customer API.
    private static async Task<Guid> ReserveAsync(HttpClient customer, ParkingSpace space, DateTimeOffset start, CancellationToken ct)
    {
        var body = new { spaceId = space.Id, start, end = start.AddHours(1), plate = TestPlates.Next() };

        using var response = await customer.PostAsJsonAsync(Relative("/api/v1/reservations"), body, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
    }

    private static async Task<string?> CustomerStateAsync(HttpClient customer, Guid id, CancellationToken ct) =>
        (await customer.GetFromJsonAsync<JsonElement>(Relative($"/api/v1/reservations/{id}"), ct)).GetProperty("state").GetString();

    private async Task<HttpClient> SignedInCustomerAsync()
    {
        var client = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(client);
        return client;
    }

    private async Task<HttpClient> SignedInAdminAsync()
    {
        var client = AuthClient.Create(factory);
        await AuthClient.SignInNewAdminAsync(factory, client);
        return client;
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}

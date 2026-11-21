using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Api.Tests.Auth;
using CityPark.Api.Tests.Lots;

namespace CityPark.Api.Tests.Reservations;

// CP-017 proved the exclusion constraint below the service; this proves it through the HTTP pipeline.
public class ParallelReservationHttpTests(ClockedApiFactory factory) : IClassFixture<ClockedApiFactory>
{
    private const int Rounds = 20;

    private static readonly Uri Reservations = new("/api/v1/reservations", UriKind.Relative);

    private static readonly HttpStatusCode[] OneWinner = [HttpStatusCode.Created, HttpStatusCode.Conflict];

    [Fact]
    public async Task TwoParallelPosts_OverlappingOnOneSpace_ReturnOne201AndOne409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var customerA = AuthClient.Create(factory);
        using var customerB = AuthClient.Create(factory);
        await AuthClient.SignInNewCustomerAsync(customerA);
        await AuthClient.SignInNewCustomerAsync(customerB);

        for (var round = 1; round <= Rounds; round++)
        {
            var lot = await TestLots.AddAsync(factory, TestLots.UniqueName("Race"), spaces: 1, inactiveSpaces: 0, ct);
            var spaceId = lot.Spaces.Single().Id;

            // 10:00–11:30 and 10:30–12:00 local; a new plate pair per round, so only the space rule (R6) can fail.
            var results = await PostTogetherAsync(
                () => customerA.PostAsJsonAsync(Reservations, Body(spaceId, "2026-12-01T09:00:00Z", "2026-12-01T10:30:00Z", $"A{round:D2}K345"), ct),
                () => customerB.PostAsJsonAsync(Reservations, Body(spaceId, "2026-12-01T09:30:00Z", "2026-12-01T11:00:00Z", $"E{round:D2}K345"), ct));

            Assert.Equal(OneWinner, results.Select(r => r.Status).Order());
            Assert.Equal("application/problem+json", Assert.Single(results, r => r.Status == HttpStatusCode.Conflict).MediaType);

            var availability = await customerA.GetFromJsonAsync<JsonElement>(
                new Uri($"/api/v1/lots/{lot.Id}/availability?start=2026-12-01T09:00:00Z&end=2026-12-01T10:30:00Z", UriKind.Relative), ct);
            Assert.Equal(0, availability.GetProperty("availableSpaces").GetInt32());
        }
    }

    // Like ParallelRunner (CP-017): the last request to arrive releases all of them together.
    private static async Task<(HttpStatusCode Status, string? MediaType)[]> PostTogetherAsync(
        params Func<Task<HttpResponseMessage>>[] posts)
    {
        var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = posts.Select(post => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref arrived) == posts.Length)
            {
                gate.SetResult();
            }

            await gate.Task;
            using var response = await post();
            return (response.StatusCode, response.Content.Headers.ContentType?.MediaType);
        })).ToArray();

        return await Task.WhenAll(tasks);
    }

    private static object Body(Guid spaceId, string start, string end, string plate) =>
        new { spaceId, start, end, plate };
}

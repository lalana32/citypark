using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CityPark.Api.Tests;

/// <summary>
/// <see cref="PostgresApiFactory"/> with "now" fixed, for endpoints whose rules depend on the time
/// (booking horizon, start in the past). Only these tests get the fixed clock: Identity also reads
/// TimeProvider, for lockout and cookie expiry.
/// </summary>
public sealed class ClockedApiFactory : PostgresApiFactory
{
    /// <summary>Tuesday 1 December 2026, 08:00 in Sarajevo (UTC+1, winter time).</summary>
    public static readonly DateTimeOffset Now = new(2026, 12, 1, 7, 0, 0, TimeSpan.Zero);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Runs after Program's registrations, so this TimeProvider replaces TimeProvider.System.
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now)));
    }
}

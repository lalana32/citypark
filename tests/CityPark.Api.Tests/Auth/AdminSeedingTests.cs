using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using CityPark.Infrastructure.Identity;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CityPark.Api.Tests.Auth;

// Each test starts its own API host on the shared database, so the seeder runs with that test's settings.
public class AdminSeedingTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task WithSettings_CreatesAnAdminWhoCanSignIn()
    {
        var email = AuthClient.NewEmail();
        await using var app = AdminSeedingHost.Start(factory, email, AuthClient.Password, out _);
        using var client = AuthClient.Create(app);

        await AuthClient.LoginExpectingSuccessAsync(client, email);
        using var me = await client.GetAsync(AuthClient.Me, TestContext.Current.CancellationToken);

        var user = await me.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["Admin"], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    // A changed Admin__Password does not rotate the password of an existing admin.
    [Fact]
    public async Task SecondStart_KeepsTheExistingAdminAndItsPassword()
    {
        var email = AuthClient.NewEmail();
        await using (AdminSeedingHost.Start(factory, email, AuthClient.Password, out _))
        {
        }

        await using var second = AdminSeedingHost.Start(factory, email, "Another-Password-7", out var logs);
        using var client = AuthClient.Create(second);

        await AuthClient.LoginExpectingSuccessAsync(client, email);
        Assert.Contains(logs.Entries, entry => entry.Contains("already exists", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seeding_NeverLogsThePassword()
    {
        var password = $"Seed-Secret-{Guid.NewGuid():N}";

        await using var app = AdminSeedingHost.Start(factory, AuthClient.NewEmail(), password, out var logs);

        Assert.Contains(logs.Entries, entry => entry.Contains("admin account was created", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(password, StringComparison.Ordinal));
    }

    // No upper-case letter and no symbol: Identity rejects it, and the start fails loudly.
    [Fact]
    public void WeakPassword_FailsTheStartWithoutRevealingThePassword()
    {
        var password = $"weak{Guid.NewGuid():N}";

        var exception = Assert.ThrowsAny<Exception>(() =>
        {
            using var app = AdminSeedingHost.Create(factory, AuthClient.NewEmail(), password, out var logs);
            try
            {
                _ = app.Services;
            }
            finally
            {
                Assert.DoesNotContain(logs.Entries, entry => entry.Contains(password, StringComparison.Ordinal));
            }
        });

        Assert.Contains("admin account could not be created", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, exception.ToString(), StringComparison.Ordinal);
    }
}

// The unreachable database proves the skip happens before any database call: the start would fail otherwise.
public class AdminSeedingSkippedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("admin@example.test", null)]
    [InlineData(null, "Only-Password-9")]
    public async Task WithoutBothSettings_SkipsSeedingAndNeverLogsThePassword(string? email, string? password)
    {
        await using var app = AdminSeedingHost.Start(factory, email, password, out var logs);
        using var client = app.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Contains(logs.Entries, entry => entry.Contains("Admin seeding skipped", StringComparison.Ordinal));
        if (password is not null)
        {
            Assert.DoesNotContain(logs.Entries, entry => entry.Contains(password, StringComparison.Ordinal));
        }
    }
}

internal static class AdminSeedingHost
{
    /// <summary>A host with the given admin settings and captured logs; it starts on first use.</summary>
    public static WebApplicationFactory<Program> Create(
        ApiFactory factory, string? email, string? password, out CapturingLoggerProvider logs)
    {
        var captured = new CapturingLoggerProvider();
        logs = captured;

        return factory.WithWebHostBuilder(builder =>
        {
            if (email is not null)
            {
                builder.UseSetting(AdminSeeder.EmailKey, email);
            }

            if (password is not null)
            {
                builder.UseSetting(AdminSeeder.PasswordKey, password);
            }

            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(captured));
        });
    }

    /// <summary>Like <see cref="Create"/>, but already started, so the seeder has run.</summary>
    public static WebApplicationFactory<Program> Start(
        ApiFactory factory, string? email, string? password, out CapturingLoggerProvider logs)
    {
        var app = Create(factory, email, password, out logs);
        _ = app.Services;
        return app;
    }
}

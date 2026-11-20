using CityPark.Application.Common;
using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CityPark.Infrastructure.Identity;

/// <summary>
/// Creates the administrator from configuration (environment variables <c>Admin__Email</c> and
/// <c>Admin__Password</c>), because there is no public admin registration (ADR-003).
/// Safe to run on every start. The password never reaches a log or an exception message.
/// </summary>
public static class AdminSeeder
{
    public const string EmailKey = "Admin:Email";

    public const string PasswordKey = "Admin:Password";

    private static readonly Action<ILogger, Exception?> LogSkipped = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1, "AdminSeedingSkipped"),
        "Admin seeding skipped: Admin__Email and Admin__Password are not both set.");

    private static readonly Action<ILogger, Exception?> LogExists = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(2, "AdminExists"),
        "Admin seeding: the admin account already exists; its password is not changed.");

    private static readonly Action<ILogger, Exception?> LogCreated = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(3, "AdminCreated"),
        "Admin seeding: the admin account was created.");

    public static async Task SeedAdminAsync(
        this IServiceProvider services,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminSeeder).FullName!);

        // Checked before the database is touched, so a start without the variables needs no database.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            LogSkipped(logger, null);
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, AppRoles.Admin))
            {
                EnsureSucceeded(await users.AddToRoleAsync(existing, AppRoles.Admin));
            }

            LogExists(logger, null);
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, result) = await users.CreateWithRoleAsync(db, email, password, AppRoles.Admin, cancellationToken);
        EnsureSucceeded(result);

        LogCreated(logger, null);
    }

    // Fails the start on purpose: a misconfigured admin should be noticed, not silently missing.
    // Only error codes, never descriptions or values, go into the message.
    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The admin account could not be created: " + string.Join(", ", result.Errors.Select(e => e.Code)) + ".");
        }
    }
}

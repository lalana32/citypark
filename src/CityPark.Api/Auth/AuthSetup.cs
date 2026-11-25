using CityPark.Application.Common;
using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;

namespace CityPark.Api.Auth;

/// <summary>ASP.NET Core Identity with the application cookie as the session (ADR-003).</summary>
public static class AuthSetup
{
    public const string CookieName = "citypark.auth";

    public static IServiceCollection AddCityParkAuthentication(this IServiceCollection services, CookieSecurePolicy securePolicy)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // An API has no login page: answer with the status code instead of a 302.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Checks the cookie's security stamp against the database on every request (default: every
        // 30 minutes), so logout rejects a copied cookie at once. Cost: one user lookup per request.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(AppRoles.Admin));

        return services;
    }
}

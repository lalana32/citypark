using System.Security.Claims;

using CityPark.Application.Common;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace CityPark.Api.Tests;

/// <summary>
/// Test-only routes that issue the real application cookie for a made-up user and require a
/// signed-in user or the Admin role, before the real login and protected endpoints exist.
/// </summary>
public sealed class AuthEndpoints : IStartupFilter
{
    /// <summary>Signs in a new user with the role given in the <c>role</c> query parameter.</summary>
    public const string SignIn = "/_test/auth/sign-in";

    public const string SignedInOnly = "/_test/auth/signed-in";

    public const string AdminOnly = "/_test/auth/admin";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(SignIn, async (HttpContext context, string role) =>
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
                    IdentityConstants.ApplicationScheme);
                await context.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));
                return TypedResults.NoContent();
            });

            endpoints.MapGet(SignedInOnly, () => TypedResults.Ok()).RequireAuthorization();

            endpoints.MapGet(AdminOnly, () => TypedResults.Ok())
                .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));
        });
    };
}

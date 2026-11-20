using CityPark.Application.Common;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CityPark.Api.Tests;

/// <summary>
/// Test-only routes that require a signed-in user or the Admin role, so 401 and 403 are tested
/// before any business endpoint needs them.
/// </summary>
public sealed class AuthEndpoints : IStartupFilter
{
    public const string SignedInOnly = "/_test/auth/signed-in";

    public const string AdminOnly = "/_test/auth/admin";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(SignedInOnly, () => TypedResults.Ok()).RequireAuthorization();

            endpoints.MapGet(AdminOnly, () => TypedResults.Ok())
                .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));
        });
    };
}

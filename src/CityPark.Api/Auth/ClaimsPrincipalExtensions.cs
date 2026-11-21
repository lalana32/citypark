using System.Globalization;
using System.Security.Claims;

namespace CityPark.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The Identity user id from the session cookie (ADR-003). Only for endpoints that require
    /// sign-in, so a missing claim is a bug, not a client error.
    /// </summary>
    public static Guid UserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The signed-in principal has no user id claim.");

        return Guid.Parse(value, CultureInfo.InvariantCulture);
    }
}

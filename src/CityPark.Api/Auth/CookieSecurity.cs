namespace CityPark.Api.Auth;

/// <summary>Whether the session and antiforgery cookies are sent only over HTTPS.</summary>
public static class CookieSecurity
{
    /// <summary>Only for the local Docker stack, which nginx serves over plain HTTP (README).</summary>
    public const string AllowHttpKey = "Cookies:AllowHttp";

    public static CookieSecurePolicy PolicyFor(IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        return environment.IsDevelopment() || configuration.GetValue<bool>(AllowHttpKey)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    }
}

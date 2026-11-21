using System.Globalization;
using System.Threading.RateLimiting;

using CityPark.Api.Errors;

using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Auth;

/// <summary>Brute-force limit on the credential endpoints: a fixed window per client IP address.</summary>
public static class AuthRateLimiting
{
    public const string CredentialsPolicy = "credentials";

    public const int PermitLimit = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddCityParkRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login and register share one budget per address. Behind a reverse proxy this is the
            // proxy's address unless forwarded headers are configured (docs/auth.md).
            options.AddPolicy(CredentialsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitLimit,
                    Window = Window,
                    QueueLimit = 0,
                }));

            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = ProblemTypes.TooManyRequests,
                Title = "Too many requests.",
                Detail = "Too many sign-in or registration attempts. Try again later.",
            },
        });
    }
}

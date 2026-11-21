using CityPark.Api.Errors;

using Microsoft.AspNetCore.Antiforgery;

namespace CityPark.Api.Auth;

/// <summary>
/// Antiforgery for the cookie session: every unsafe request needs the token from
/// <c>GET /api/v1/auth/csrf</c> in the <see cref="HeaderName"/> header.
/// </summary>
public static class CsrfProtection
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public const string CookieName = "citypark.csrf";

    private const string InvalidTokenDetail =
        $"The {HeaderName} header is missing or invalid. Get a token from GET /api/v1/auth/csrf.";

    public static IServiceCollection AddCityParkCsrfProtection(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        return services;
    }

    /// <summary>Rejects POST, PUT, PATCH and DELETE in the group without a valid token, before the handler runs.</summary>
    public static RouteGroupBuilder RequireCsrfToken(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.AddEndpointFilter(ValidateAsync);
    }

    private static async ValueTask<object?> ValidateAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var method = http.Request.Method;

        // Checked first: IsRequestValidAsync throws on plain HTTP when the cookie is Secure, even for GET.
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return await next(context);
        }

        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        if (await antiforgery.IsRequestValidAsync(http))
        {
            return await next(context);
        }

        return TypedResults.Problem(new HttpValidationProblemDetails(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [ApiExceptionHandler.GeneralErrorKey] = [InvalidTokenDetail] })
        {
            Status = StatusCodes.Status400BadRequest,
            Type = ProblemTypes.BadRequest,
            Title = "The request is invalid.",
            Detail = InvalidTokenDetail,
        });
    }
}

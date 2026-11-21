using CityPark.Application.Availability;
using CityPark.Application.Lots;

using Microsoft.Extensions.DependencyInjection;

namespace CityPark.Application;

public static class DependencyInjection
{
    /// <summary>The city's time zone for every business rule (ADR-004).</summary>
    public const string CityTimeZoneId = "Europe/Sarajevo";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        // Resolved once at startup: an image without time zone data fails here, not on the first request.
        services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(CityTimeZoneId));

        services.AddScoped<LotQueryService>();
        services.AddScoped<AvailabilityService>();

        return services;
    }
}

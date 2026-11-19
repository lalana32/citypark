using CityPark.Application.Persistence;
using CityPark.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace CityPark.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>(options => AppDbContextOptions.Configure(options, connectionString));

        // Same scoped instance under both types: a use case and the API share one unit of work per request.
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddSingleton<IDatabaseErrors, PostgresDatabaseErrors>();

        return services;
    }
}

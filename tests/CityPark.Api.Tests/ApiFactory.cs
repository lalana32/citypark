using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CityPark.Api.Tests;

/// <summary>
/// The real API in memory, in the Production environment, with throwing test endpoints under
/// <see cref="ThrowingEndpoints.Prefix"/> and a validating route at <see cref="ValidationEndpoints.Reservations"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Nothing listens on port 1, so every database call fails at once.
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=citypark;Username=citypark;Password=citypark;Timeout=2";

    protected virtual string ConnectionString => UnreachableDatabase;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:CityPark", ConnectionString);
        builder.ConfigureServices(services => services
            .AddSingleton<IStartupFilter, ThrowingEndpoints>()
            .AddSingleton<IStartupFilter, ValidationEndpoints>());
    }
}

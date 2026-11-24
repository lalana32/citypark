using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CityPark.Api.Tests;

/// <summary>
/// The real API in memory, in the Production environment, with throwing test endpoints under
/// <see cref="ThrowingEndpoints.Prefix"/>, a validating route at <see cref="ValidationEndpoints.Reservations"/>
/// and cookie routes in <see cref="AuthEndpoints"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Nothing listens on port 1, so every database call fails at once.
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=citypark;Username=citypark;Password=citypark;Timeout=2";

    protected virtual string ConnectionString => UnreachableDatabase;

    /// <summary>False keeps the test-only routes out, so the OpenAPI document is the real contract.</summary>
    protected virtual bool AddTestEndpoints => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:CityPark", ConnectionString);
        // Admin__Email or Admin__Password set on the developer machine must not reach the tests.
        builder.UseSetting("Admin:Email", string.Empty);
        builder.UseSetting("Admin:Password", string.Empty);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientAddress>();
            if (AddTestEndpoints)
            {
                services
                    .AddSingleton<IStartupFilter, ThrowingEndpoints>()
                    .AddSingleton<IStartupFilter, ValidationEndpoints>()
                    .AddSingleton<IStartupFilter, AuthEndpoints>();
            }
        });
    }
}

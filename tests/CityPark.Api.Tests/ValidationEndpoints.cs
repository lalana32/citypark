using CityPark.Api.Reservations;
using CityPark.Api.Validation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CityPark.Api.Tests;

/// <summary>
/// A test-only route that binds and validates a real request DTO through minimal API binding,
/// so the tests see the same 400 responses a business endpoint will produce.
/// </summary>
public sealed class ValidationEndpoints : IStartupFilter
{
    public const string Reservations = "/_test/validation/reservations";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints
            .MapPost(Reservations, (CreateReservationRequest request) => TypedResults.Ok(request))
            .WithValidation<CreateReservationRequest>());
    };
}

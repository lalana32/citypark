using CityPark.Api.Reservations;
using CityPark.Api.Validation;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CityPark.Api.Tests;

/// <summary>
/// Test-only routes that bind and validate a real request DTO through minimal API binding,
/// so the tests see the same 400 and 422 responses a business endpoint will produce.
/// </summary>
public sealed class ValidationEndpoints : IStartupFilter
{
    /// <summary>Checks only the shape of the body.</summary>
    public const string Reservations = "/_test/validation/reservations";

    /// <summary>Checks the shape, then the domain booking rules, the way the create use case will.</summary>
    public const string BookableReservations = "/_test/validation/bookable-reservations";

    private static readonly TimeZoneInfo Sarajevo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static readonly ParkingLot Lot = ParkingLot.Create(
        "Centar", "Ferhadija 1", new Rate(2.00m, "BAM"), new OpeningHours(new TimeOnly(7, 0), new TimeOnly(22, 0)));

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapPost(Reservations, (CreateReservationRequest request) => TypedResults.Ok(request))
                .WithValidation<CreateReservationRequest>();

            endpoints
                .MapPost(BookableReservations, (CreateReservationRequest request) =>
                {
                    // Start and End are not null here: the validation filter has already run.
                    var interval = new TimeInterval(request.Start!.Value, request.End!.Value);
                    Reservation.EnsureBookable(Lot, interval, Sarajevo, TimeProvider.System);
                    return TypedResults.Ok(request);
                })
                .WithValidation<CreateReservationRequest>();
        });
    };
}

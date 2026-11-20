using CityPark.Application.Common;
using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CityPark.Api.Tests;

/// <summary>
/// Test-only routes that throw a chosen exception. They are added after the real pipeline,
/// so the exception passes through the API's own exception handler.
/// </summary>
public sealed class ThrowingEndpoints : IStartupFilter
{
    public const string Prefix = "/_test/throw";

    public const string SecretMessage = "Connection to 10.0.0.5 failed with password hunter2.";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Map(Prefix, branch => branch.Run(context => throw Create(context.Request.Path.Value)));
    };

    private static Exception Create(string? kind) => kind switch
    {
        "/request-validation" => new RequestValidationException("Page must be 1 or greater, but is 0."),
        "/not-found" => new NotFoundException("Parking lot 42 was not found."),
        "/domain" => new DomainException("A reservation must last at least 30 minutes."),
        "/domain-conflict" => new DomainConflictException("Reservation 7 is already cancelled."),
        "/reservation-conflict" => new ReservationConflictException(
            Guid.Empty,
            new TimeInterval(
                new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero))),
        _ => new InvalidOperationException(SecretMessage),
    };
}

using System.Text.Json;

using CityPark.Application.Admin;
using CityPark.Application.Common;
using CityPark.Domain.Common;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Errors;

/// <summary>Turns every exception that leaves an endpoint into a ProblemDetails response.</summary>
public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Errors that belong to no single field are listed under this key.</summary>
    public const string GeneralErrorKey = "general";

    /// <summary>The R12 conflict lists the reservations that block a deactivation under this key.</summary>
    public const string ReservationIdsKey = "reservationIds";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // More specific types first: each one derives from the next (DomainException is the base).
        var problem = exception switch
        {
            SpaceHasActiveReservationsException blocked => BlockedDeactivation(blocked),
            RequestValidationException validation => Validation(
                StatusCodes.Status400BadRequest, ProblemTypes.BadRequest, "The request is invalid.", exception.Message, validation.Errors),
            BadHttpRequestException badRequest => Unreadable(badRequest),
            NotFoundException => Problem(
                StatusCodes.Status404NotFound, ProblemTypes.NotFound, "The resource was not found.", exception.Message),
            DomainConflictException => Problem(
                StatusCodes.Status409Conflict, ProblemTypes.Conflict, "The request conflicts with the current state.", exception.Message),
            DomainException domain => Validation(
                StatusCodes.Status422UnprocessableEntity,
                ProblemTypes.UnprocessableEntity,
                "A business rule was violated.",
                exception.Message,
                FieldErrors(domain.Field, exception.Message)),
            _ => Unexpected(httpContext, exception),
        };

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string type, string title, string detail) =>
        new() { Status = status, Type = type, Title = title, Detail = detail };

    private static HttpValidationProblemDetails Validation(
        int status,
        string type,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(errors ?? new Dictionary<string, string[]> { [GeneralErrorKey] = [detail] })
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
        };

    private static ProblemDetails BlockedDeactivation(SpaceHasActiveReservationsException exception)
    {
        var problem = Problem(
            StatusCodes.Status409Conflict, ProblemTypes.Conflict, "The request conflicts with the current state.", exception.Message);
        problem.Extensions[ReservationIdsKey] = exception.ReservationIds;
        return problem;
    }

    // The domain names fields in C# (Start); the client sees the JSON name it sent (start).
    private static Dictionary<string, string[]>? FieldErrors(string? field, string message) =>
        field is null ? null : new() { [JsonNamingPolicy.CamelCase.ConvertName(field)] = [message] };

    // Minimal API binding throws this for a body that is not JSON or has a value of the wrong type
    // (RouteHandlerOptions.ThrowOnBadRequest), and also for 413 or 415.
    private static ProblemDetails Unreadable(BadHttpRequestException exception)
    {
        if (exception.StatusCode != StatusCodes.Status400BadRequest)
        {
            return new ProblemDetails { Status = exception.StatusCode };
        }

        // The framework message names C# parameter types, so the client gets its own text instead.
        var errors = exception.InnerException is JsonException { Path: { } path } && path.StartsWith("$.", StringComparison.Ordinal)
            ? new Dictionary<string, string[]> { [path[2..]] = ["The value has the wrong JSON type or format."] }
            : new Dictionary<string, string[]> { [GeneralErrorKey] = ["The request body is missing or is not valid JSON."] };

        return Validation(
            StatusCodes.Status400BadRequest, ProblemTypes.BadRequest, "The request is invalid.", "The request could not be read.", errors);
    }

    private ProblemDetails Unexpected(HttpContext httpContext, Exception exception)
    {
        LogUnexpected(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = ProblemTypes.InternalServerError,
            Title = "An unexpected error occurred.",
            // Exception text and stack trace only on a developer machine; elsewhere the log has them.
            Detail = environment.IsDevelopment() ? exception.ToString() : null,
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected exception for {Method} {Path}")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string method, string path);
}

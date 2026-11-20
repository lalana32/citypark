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

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // DomainConflictException derives from DomainException, so it must be matched first.
        var problem = exception switch
        {
            RequestValidationException => Validation(
                StatusCodes.Status400BadRequest, ProblemTypes.BadRequest, "The request is invalid.", exception.Message),
            NotFoundException => Problem(
                StatusCodes.Status404NotFound, ProblemTypes.NotFound, "The resource was not found.", exception.Message),
            DomainConflictException => Problem(
                StatusCodes.Status409Conflict, ProblemTypes.Conflict, "The request conflicts with the current state.", exception.Message),
            DomainException => Validation(
                StatusCodes.Status422UnprocessableEntity, ProblemTypes.UnprocessableEntity, "A business rule was violated.", exception.Message),
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

    private static HttpValidationProblemDetails Validation(int status, string type, string title, string detail) =>
        new(new Dictionary<string, string[]> { [GeneralErrorKey] = [detail] })
        {
            Status = status,
            Type = type,
            Title = title,
            Detail = detail,
        };

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

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using CityPark.Api.Errors;
using CityPark.Application.Common;

namespace CityPark.Api.Validation;

/// <summary>Checks the DataAnnotations of the <typeparamref name="TRequest"/> argument before the handler runs.</summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The endpoint has no {typeof(TRequest).Name} argument to validate.");

        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            throw new RequestValidationException("One or more fields are invalid.", ToErrors(results));
        }

        return next(context);
    }

    // Keys use the JSON (camelCase) names the client sent, not the C# property names.
    private static Dictionary<string, string[]> ToErrors(List<ValidationResult> results) =>
        results
            .SelectMany(
                result => result.MemberNames.DefaultIfEmpty(ApiExceptionHandler.GeneralErrorKey),
                (result, member) => (
                    Field: JsonNamingPolicy.CamelCase.ConvertName(member),
                    Message: result.ErrorMessage ?? "The value is invalid."))
            .GroupBy(error => error.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal);
}

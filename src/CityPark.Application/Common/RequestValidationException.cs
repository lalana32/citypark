namespace CityPark.Application.Common;

/// <summary>
/// A use case got input that no request can sensibly mean, for example page 0.
/// Unlike a DomainException it breaks no business rule. The API maps it to 400 Bad Request.
/// </summary>
public class RequestValidationException : Exception
{
    public RequestValidationException(string message)
        : base(message)
    {
    }

    public RequestValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Errors = errors;
    }

    /// <summary>Field name to messages; null when the error belongs to no single field.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}

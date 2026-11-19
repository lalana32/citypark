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
}

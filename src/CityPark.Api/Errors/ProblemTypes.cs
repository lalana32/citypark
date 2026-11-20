namespace CityPark.Api.Errors;

/// <summary>The <c>type</c> URIs of the error contract (docs/api-conventions.md).</summary>
internal static class ProblemTypes
{
    public const string BadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    public const string NotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    public const string Conflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    public const string UnprocessableEntity = "https://tools.ietf.org/html/rfc9110#section-15.5.21";
    public const string InternalServerError = "https://tools.ietf.org/html/rfc9110#section-15.6.1";
}

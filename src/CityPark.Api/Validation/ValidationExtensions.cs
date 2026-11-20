namespace CityPark.Api.Validation;

public static class ValidationExtensions
{
    /// <summary>Validates the request body before the handler runs and documents the 400 response.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}

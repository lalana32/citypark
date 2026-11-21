using CityPark.Api.Auth;

namespace CityPark.Api;

public static class ApiRoutes
{
    public const string V1 = "/api/v1";

    /// <summary>The group that every business endpoint is mapped on.</summary>
    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder app) =>
        app.MapGroup(V1)
            .RequireCsrfToken()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
}

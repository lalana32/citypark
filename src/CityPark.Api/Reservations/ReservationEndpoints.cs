using System.Security.Claims;

using CityPark.Api.Auth;
using CityPark.Api.Validation;
using CityPark.Application.Common;
using CityPark.Application.Reservations;

using Microsoft.AspNetCore.Http.HttpResults;

namespace CityPark.Api.Reservations;

public static class ReservationEndpoints
{
    public const string Route = ApiRoutes.V1 + "/reservations";

    public const int DefaultPageSize = 20;

    public static RouteGroupBuilder MapReservationEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        // The caller is always the owner: the user id comes from the cookie, never from the request.
        var reservations = api.MapGroup("/reservations")
            .WithTags("Reservations")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reservations.MapPost(string.Empty, CreateAsync)
            .WithValidation<CreateReservationRequest>()
            .WithSummary("Reserves a space for the signed-in customer; the price is fixed now (R8).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        reservations.MapGet(string.Empty, ListAsync)
            .WithSummary("The signed-in customer's reservations, newest start first, one page at a time.")
            .ProducesValidationProblem();

        reservations.MapGet("/{id:guid}", GetAsync)
            .WithSummary("One of the signed-in customer's reservations. Another user's reservation is not found.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    public static Uri Location(Guid id) => new($"{Route}/{id}", UriKind.Relative);

    private static async Task<Created<ReservationResponse>> CreateAsync(
        CreateReservationRequest request,
        ClaimsPrincipal principal,
        CreateReservationService create,
        OwnReservationQueryService own,
        CancellationToken ct)
    {
        var userId = principal.UserId();

        // ValidationFilter has already rejected missing fields.
        var reservation = await create.CreateAsync(
            userId, request.SpaceId!.Value, request.Start!.Value, request.End!.Value, request.Plate!, ct);

        // Read back through the query, so the 201 body is exactly what GET on the Location returns.
        var created = await own.GetAsync(reservation.Id, userId, ct);

        return TypedResults.Created(Location(reservation.Id), ReservationResponse.From(created));
    }

    private static async Task<Ok<PagedResult<ReservationResponse>>> ListAsync(
        ClaimsPrincipal principal,
        OwnReservationQueryService own,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await own.ListAsync(principal.UserId(), new PageRequest(page, pageSize), ct);

        return TypedResults.Ok(new PagedResult<ReservationResponse>(
            [.. result.Items.Select(ReservationResponse.From)], result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<Ok<ReservationResponse>> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        OwnReservationQueryService own,
        CancellationToken ct) =>
        TypedResults.Ok(ReservationResponse.From(await own.GetAsync(id, principal.UserId(), ct)));
}

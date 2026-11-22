using System.Security.Claims;

using CityPark.Api.Auth;
using CityPark.Api.OpenApi;
using CityPark.Api.Validation;
using CityPark.Application.Common;
using CityPark.Application.Reservations;
using CityPark.Domain.Reservations;

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
            .WithSummary("Reserves the given space, or the first free space of the given lot, for the signed-in customer; the price is fixed now (R8).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithResponseExample(StatusCodes.Status201Created, ReservationExamples.Reservation)
            .WithResponseExample(StatusCodes.Status409Conflict, ReservationExamples.Conflict, "application/problem+json");

        reservations.MapGet(string.Empty, ListAsync)
            .WithSummary("The signed-in customer's reservations, newest start first, one page at a time.")
            .ProducesValidationProblem()
            .WithResponseExample(StatusCodes.Status200OK, ReservationExamples.Page);

        reservations.MapGet("/{id:guid}", GetAsync)
            .WithSummary("One of the signed-in customer's reservations. Another user's reservation is not found.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithResponseExample(StatusCodes.Status200OK, ReservationExamples.Reservation);

        reservations.MapPost("/{id:guid}/cancel", CancelAsync)
            .WithSummary("Cancels one of the signed-in customer's reservations, at any time before it starts (R9).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithResponseExample(StatusCodes.Status422UnprocessableEntity, ReservationExamples.CancelAfterStart, "application/problem+json");

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

        // ValidationFilter has already rejected missing fields and both or neither of SpaceId and LotId.
        var reservation = request.LotId is { } lotId
            ? await create.CreateInLotAsync(userId, lotId, request.Start!.Value, request.End!.Value, request.Plate!, ct)
            : await create.CreateAsync(userId, request.SpaceId!.Value, request.Start!.Value, request.End!.Value, request.Plate!, ct);

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

    private static async Task<NoContent> CancelAsync(
        Guid id,
        ClaimsPrincipal principal,
        CancelReservationService cancel,
        CancellationToken ct)
    {
        // Always as a customer, also for an admin: this route acts only on the caller's own reservations.
        await cancel.CancelAsync(id, new Actor(principal.UserId(), CancellationActor.Customer), ct);

        return TypedResults.NoContent();
    }
}

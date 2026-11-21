using CityPark.Api.Auth;
using CityPark.Api.Lots;
using CityPark.Api.OpenApi;
using CityPark.Api.Validation;
using CityPark.Application.Admin;
using CityPark.Application.Lots;

using Microsoft.AspNetCore.Http.HttpResults;

namespace CityPark.Api.Admin;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        // The policy runs before the body is read, so a customer gets 403 even for an invalid body.
        var admin = api.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPost("/lots", CreateLotAsync)
            .WithValidation<LotRequest>()
            .WithSummary("Creates a lot without spaces. The Location is the public lot route.")
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithResponseExample(StatusCodes.Status201Created, LotExamples.Lot)
            .WithResponseExample(StatusCodes.Status422UnprocessableEntity, AdminExamples.InvalidWindow, "application/problem+json");

        admin.MapPut("/lots/{id:guid}", UpdateLotAsync)
            .WithValidation<LotRequest>()
            .WithSummary("Replaces a lot's name, address, rate and opening hours. Existing reservations keep their price (R13).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithResponseExample(StatusCodes.Status200OK, LotExamples.Lot);

        admin.MapPost("/lots/{id:guid}/spaces", AddSpaceAsync)
            .WithValidation<CreateSpaceRequest>()
            .WithSummary("Adds an active space to a lot. The label must be unique in the lot (I1).")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithResponseExample(StatusCodes.Status201Created, AdminExamples.Space)
            .WithResponseExample(StatusCodes.Status409Conflict, AdminExamples.DuplicateLabel, "application/problem+json");

        admin.MapPost("/lots/{lotId:guid}/spaces/{spaceId:guid}/deactivate", DeactivateSpaceAsync)
            .WithSummary("Takes a space out of service. Rejected while it has Active reservations that have not ended (R12).")
            .WithDescription("The 409 response lists those reservations in reservationIds; cancel them first.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithResponseExample(StatusCodes.Status409Conflict, AdminExamples.SpaceHasReservations, "application/problem+json");

        return api;
    }

    private static async Task<Created<LotResponse>> CreateLotAsync(
        LotRequest request,
        AdminLotService admin,
        LotQueryService lots,
        CancellationToken ct)
    {
        var lot = await admin.CreateLotAsync(request.ToDetails(), ct);

        // Read back through the public query, so the 201 body is exactly what GET on the Location returns.
        return TypedResults.Created(LotEndpoints.Location(lot.Id), LotResponse.From(await lots.GetAsync(lot.Id, ct)));
    }

    private static async Task<Ok<LotResponse>> UpdateLotAsync(
        Guid id,
        LotRequest request,
        AdminLotService admin,
        LotQueryService lots,
        CancellationToken ct)
    {
        await admin.UpdateLotAsync(id, request.ToDetails(), ct);

        return TypedResults.Ok(LotResponse.From(await lots.GetAsync(id, ct)));
    }

    // No Location: there is no route that returns one space.
    private static async Task<Created<SpaceResponse>> AddSpaceAsync(
        Guid id,
        CreateSpaceRequest request,
        AdminLotService admin,
        CancellationToken ct) =>
        TypedResults.Created((string?)null, SpaceResponse.From(await admin.AddSpaceAsync(id, request.Label!, ct)));

    private static async Task<NoContent> DeactivateSpaceAsync(
        Guid lotId,
        Guid spaceId,
        AdminLotService admin,
        CancellationToken ct)
    {
        await admin.DeactivateSpaceAsync(lotId, spaceId, ct);

        return TypedResults.NoContent();
    }
}

using CityPark.Api.OpenApi;
using CityPark.Api.Validation;
using CityPark.Application.Availability;
using CityPark.Application.Common;
using CityPark.Application.Lots;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Lots;

public static class LotEndpoints
{
    public const string Route = ApiRoutes.V1 + "/lots";

    public const int DefaultPageSize = 20;

    public static RouteGroupBuilder MapLotEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        // Explicit, so a future fallback policy that requires sign-in cannot close the public catalogue.
        var lots = api.MapGroup("/lots").WithTags("Lots").AllowAnonymous();

        lots.MapGet(string.Empty, ListAsync)
            .WithSummary("Lists parking lots by name, one page at a time.")
            .ProducesValidationProblem()
            .WithResponseExample(StatusCodes.Status200OK, LotExamples.Page);

        lots.MapGet("/{id:guid}", GetAsync)
            .WithSummary("One parking lot.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithResponseExample(StatusCodes.Status200OK, LotExamples.Lot);

        lots.MapGet("/{id:guid}/availability", GetAvailabilityAsync)
            .WithValidation<AvailabilityQuery>()
            .WithSummary("Free spaces of a lot for one interval on the 15-minute grid.")
            .WithDescription(
                "start and end are ISO 8601 instants with an offset, for example 2026-12-01T09:00:00Z. " +
                "Encode + as %2B in the query string. The response echoes them with the Europe/Sarajevo offset.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithParameterExample("start", LotExamples.Start)
            .WithParameterExample("end", LotExamples.End)
            .WithResponseExample(StatusCodes.Status200OK, LotExamples.Availability)
            .WithResponseExample(StatusCodes.Status422UnprocessableEntity, LotExamples.OffGrid, "application/problem+json");

        lots.MapGet("/{id:guid}/quote", GetQuoteAsync)
            .WithValidation<AvailabilityQuery>()
            .WithSummary("The price a reservation of the lot for one interval would get; every started hour is billed (R8).")
            .WithDescription(
                "Read-only: nothing is reserved and free spaces are not checked. The interval must be bookable " +
                "(15-minute grid, at least 30 minutes, opening hours), so the quote equals the price of a reservation " +
                "made for the same interval while the rate is unchanged. Encode + as %2B in the query string.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .WithParameterExample("start", LotExamples.Start)
            .WithParameterExample("end", LotExamples.QuoteEnd)
            .WithResponseExample(StatusCodes.Status200OK, LotExamples.Quote)
            .WithResponseExample(StatusCodes.Status422UnprocessableEntity, LotExamples.OffGrid, "application/problem+json");

        return api;
    }

    public static Uri Location(Guid id) => new($"{Route}/{id}", UriKind.Relative);

    private static async Task<Ok<PagedResult<LotResponse>>> ListAsync(
        LotQueryService lots,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await lots.ListAsync(new PageRequest(page, pageSize), ct);

        return TypedResults.Ok(new PagedResult<LotResponse>(
            [.. result.Items.Select(LotResponse.From)], result.Page, result.PageSize, result.TotalCount));
    }

    private static async Task<Ok<LotResponse>> GetAsync(Guid id, LotQueryService lots, CancellationToken ct) =>
        TypedResults.Ok(LotResponse.From(await lots.GetAsync(id, ct)));

    private static async Task<Ok<AvailabilityResponse>> GetAvailabilityAsync(
        Guid id,
        [AsParameters] AvailabilityQuery query,
        AvailabilityService availability,
        [FromServices] TimeZoneInfo timeZone,
        CancellationToken ct)
    {
        // ValidationFilter has already rejected a missing start or end.
        var start = query.Start!.Value;
        var end = query.End!.Value;

        var result = await availability.GetAvailabilityAsync(id, start, end, ct);

        return TypedResults.Ok(AvailabilityResponse.From(
            id, TimeZoneInfo.ConvertTime(start, timeZone), TimeZoneInfo.ConvertTime(end, timeZone), result));
    }

    private static async Task<Ok<QuoteResponse>> GetQuoteAsync(
        Guid id,
        [AsParameters] AvailabilityQuery query,
        QuoteService quotes,
        [FromServices] TimeZoneInfo timeZone,
        CancellationToken ct)
    {
        // ValidationFilter has already rejected a missing start or end.
        var start = query.Start!.Value;
        var end = query.End!.Value;

        var quote = await quotes.GetQuoteAsync(id, start, end, ct);

        return TypedResults.Ok(QuoteResponse.From(
            id, TimeZoneInfo.ConvertTime(start, timeZone), TimeZoneInfo.ConvertTime(end, timeZone), quote));
    }
}

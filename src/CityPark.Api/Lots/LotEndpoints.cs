using CityPark.Application.Common;
using CityPark.Application.Lots;

using Microsoft.AspNetCore.Http.HttpResults;

namespace CityPark.Api.Lots;

public static class LotEndpoints
{
    public const int DefaultPageSize = 20;

    public static RouteGroupBuilder MapLotEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        // Explicit, so a future fallback policy that requires sign-in cannot close the public catalogue.
        var lots = api.MapGroup("/lots").WithTags("Lots").AllowAnonymous();

        lots.MapGet(string.Empty, ListAsync)
            .WithSummary("Lists parking lots by name, one page at a time.")
            .ProducesValidationProblem();

        lots.MapGet("/{id:guid}", GetAsync)
            .WithSummary("One parking lot.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

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
}

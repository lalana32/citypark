using System.Globalization;

using CityPark.Api.Errors;
using CityPark.Application.Common;

using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Lots;

/// <summary>Example bodies for the OpenAPI document, built from the real response types.</summary>
internal static class LotExamples
{
    public const string Start = "2026-12-01T10:00:00+01:00";

    public const string End = "2026-12-01T11:00:00+01:00";

    private static readonly Guid LotId = Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01");

    public static readonly LotResponse Lot = new(
        LotId, "Centar", "Ferhadija 1", 2.50m, "BAM", new TimeOnly(7, 0), new TimeOnly(22, 0), 30);

    public static readonly PagedResult<LotResponse> Page = new([Lot], Page: 1, PageSize: 20, TotalCount: 1);

    public static readonly AvailabilityResponse Availability = new(
        LotId,
        DateTimeOffset.Parse(Start, CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(End, CultureInfo.InvariantCulture),
        TotalSpaces: 30,
        AvailableSpaces: 2,
        [
            new FreeSpaceResponse(Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02"), "A-02"),
            new FreeSpaceResponse(Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e17"), "A-17"),
        ]);

    public static readonly HttpValidationProblemDetails OffGrid = new(
        new Dictionary<string, string[]>
        {
            ["start"] = ["Start (10:10:00) must fall on the 15-minute grid (:00, :15, :30, :45) (R1)."],
        })
    {
        Type = ProblemTypes.UnprocessableEntity,
        Title = "A business rule was violated.",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = "Start (10:10:00) must fall on the 15-minute grid (:00, :15, :30, :45) (R1).",
    };
}

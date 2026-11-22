using CityPark.Api.Errors;
using CityPark.Application.Common;
using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Admin;

/// <summary>Example bodies for the OpenAPI document, built from the real response types.</summary>
internal static class AdminExamples
{
    private const string BadWindow = "ClosesAt (07:00) must be after OpensAt (22:00).";

    public static readonly AdminReservationResponse CancelledReservation = new(
        Guid.Parse("019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01"),
        Guid.Parse("019ad0a3-2f1e-7c4d-8b5a-6e7f8091a2b3"),
        Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7d01"),
        "Centar",
        Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02"),
        "A-02",
        new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 1, 10, 30, 0, TimeSpan.Zero),
        "A12K345",
        5.00m,
        "BAM",
        ReservationStatus.Cancelled,
        ReservationState.Cancelled);

    public static readonly PagedResult<AdminReservationResponse> ReservationPage =
        new([CancelledReservation], Page: 1, PageSize: 20, TotalCount: 1);

    public static readonly SpaceResponse Space =new(Guid.Parse("019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e31"), "B-01", true);

    public static readonly HttpValidationProblemDetails InvalidWindow = new(
        new Dictionary<string, string[]> { ["closesAt"] = [BadWindow] })
    {
        Type = ProblemTypes.UnprocessableEntity,
        Title = "A business rule was violated.",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = BadWindow,
    };

    public static readonly ProblemDetails DuplicateLabel = new()
    {
        Type = ProblemTypes.Conflict,
        Title = "The request conflicts with the current state.",
        Status = StatusCodes.Status409Conflict,
        Detail = "Lot 'Centar' already has a space labelled 'B-01'.",
    };

    public static readonly ProblemDetails SpaceHasReservations = new()
    {
        Type = ProblemTypes.Conflict,
        Title = "The request conflicts with the current state.",
        Status = StatusCodes.Status409Conflict,
        Detail = "Space 019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02 has 1 active reservation(s) that have not ended; cancel them first.",
        Extensions = { [ApiExceptionHandler.ReservationIdsKey] = new[] { Guid.Parse("019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01") } },
    };
}

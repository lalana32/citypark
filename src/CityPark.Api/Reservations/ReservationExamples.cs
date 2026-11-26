using CityPark.Api.Errors;
using CityPark.Application.Common;
using CityPark.Domain.Reservations;

using Microsoft.AspNetCore.Mvc;

namespace CityPark.Api.Reservations;

/// <summary>Example bodies for the OpenAPI document, built from the real response types.</summary>
internal static class ReservationExamples
{
    private const string NotBeforeStart = "A customer can cancel a reservation only before it starts (R9).";

    private static readonly Guid ReservationId = Guid.Parse("019ad0a5-1b2c-7d3e-8f40-5a6b7c8d9e01");

    public static readonly ReservationResponse Reservation = new(
        ReservationId,
        "Centar",
        "A-02",
        new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 1, 10, 30, 0, TimeSpan.Zero),
        "A12K345",
        5.00m,
        "BAM",
        ReservationState.Upcoming);

    public static readonly PagedResult<ReservationResponse> Page = new([Reservation], Page: 1, PageSize: 20, TotalCount: 1);

    public static readonly ProblemDetails Conflict = new()
    {
        Type = ProblemTypes.Conflict,
        Title = "The request conflicts with the current state.",
        Status = StatusCodes.Status409Conflict,
        Detail = "Space 019ad0a4-6c1e-7a3b-9f2d-3e4c5b6a7e02 already has an active reservation that overlaps " +
            "2026-12-01T09:00:00.0000000+00:00 – 2026-12-01T10:30:00.0000000+00:00.",
        Extensions = { [ApiExceptionHandler.ConflictCodeKey] = ConflictCodes.SpaceTaken },
    };

    public static readonly HttpValidationProblemDetails CancelAfterStart = new(
        new Dictionary<string, string[]> { [ApiExceptionHandler.GeneralErrorKey] = [NotBeforeStart] })
    {
        Type = ProblemTypes.UnprocessableEntity,
        Title = "A business rule was violated.",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = NotBeforeStart,
    };
}

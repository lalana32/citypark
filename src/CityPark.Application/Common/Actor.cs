using CityPark.Domain.Reservations;

namespace CityPark.Application.Common;

/// <summary>The signed-in user a use case acts for. The API builds it from the sign-in cookie (ADR-003).</summary>
public sealed record Actor(Guid UserId, CancellationActor Role);

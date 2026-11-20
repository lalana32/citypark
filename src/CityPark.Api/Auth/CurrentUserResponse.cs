namespace CityPark.Api.Auth;

public sealed record CurrentUserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

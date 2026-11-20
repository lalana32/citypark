using System.ComponentModel.DataAnnotations;

namespace CityPark.Api.Auth;

/// <summary>Body for signing in. No email format check: a wrong address simply gets the same 401.</summary>
public sealed class LoginRequest
{
    [Required]
    public string? Email { get; init; }

    [Required]
    public string? Password { get; init; }
}

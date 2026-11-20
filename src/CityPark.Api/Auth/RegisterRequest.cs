using System.ComponentModel.DataAnnotations;

namespace CityPark.Api.Auth;

/// <summary>Body for creating a customer account. Password strength is Identity's rule (422), not shape (400).</summary>
public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; init; }

    [Required]
    public string? Password { get; init; }
}

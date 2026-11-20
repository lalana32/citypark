using System.Security.Claims;

using CityPark.Api.Errors;
using CityPark.Api.Validation;
using CityPark.Application.Common;
using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityPark.Api.Auth;

public static class AuthenticationEndpoints
{
    public const string Me = ApiRoutes.V1 + "/auth/me";

    public static RouteGroupBuilder MapAuthenticationEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var auth = api.MapGroup("/auth").WithTags("Authentication");

        auth.MapPost("/register", RegisterAsync)
            .WithValidation<RegisterRequest>()
            .WithSummary("Creates a customer account. It does not sign in.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        auth.MapPost("/login", LoginAsync)
            .WithValidation<LoginRequest>()
            .WithSummary("Signs in and sets the session cookie.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithSummary("Signs out and invalidates every copy of the session cookie.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithSummary("The signed-in user and their roles.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static async Task<Results<Created<CurrentUserResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        UserManager<IdentityUser<Guid>> users,
        AppDbContext db,
        CancellationToken ct)
    {
        // User and role in one transaction, so a failure cannot leave an account without a role.
        // The retry strategy (ADR-002) replays the whole block, so it starts from an empty change tracker.
        var (user, result) = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var newUser = new IdentityUser<Guid> { Id = Guid.CreateVersion7(), UserName = request.Email, Email = request.Email };

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var outcome = await users.CreateAsync(newUser, request.Password!);
            if (outcome.Succeeded)
            {
                outcome = await users.AddToRoleAsync(newUser, AppRoles.Customer);
            }

            if (outcome.Succeeded)
            {
                await transaction.CommitAsync(ct);
            }

            return (newUser, outcome);
        });

        if (!result.Succeeded)
        {
            return RegistrationFailed(result);
        }

        return TypedResults.Created(Me, await CurrentUserAsync(users, user));
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        SignInManager<IdentityUser<Guid>> signInManager)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email!, request.Password!, isPersistent: false, lockoutOnFailure: true);

        // Unknown email, wrong password and locked account get the same answer.
        if (!result.Succeeded)
        {
            return TypedResults.Problem(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Type = ProblemTypes.Unauthorized,
                Title = "Authentication failed.",
                Detail = "The email or password is incorrect.",
            });
        }

        var users = signInManager.UserManager;
        var user = await users.FindByNameAsync(request.Email!)
            ?? throw new InvalidOperationException("The user that just signed in was not found.");

        return TypedResults.Ok(await CurrentUserAsync(users, user));
    }

    private static async Task<NoContent> LogoutAsync(
        ClaimsPrincipal principal,
        SignInManager<IdentityUser<Guid>> signInManager)
    {
        // Deleting the browser's cookie is not enough: a copied cookie would still be valid.
        // A new security stamp makes the validator reject every old copy (AuthSetup).
        var user = await signInManager.UserManager.GetUserAsync(principal);
        if (user is not null)
        {
            await signInManager.UserManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(
        ClaimsPrincipal principal,
        UserManager<IdentityUser<Guid>> users)
    {
        var user = await users.GetUserAsync(principal);

        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(await CurrentUserAsync(users, user));
    }

    private static async Task<CurrentUserResponse> CurrentUserAsync(UserManager<IdentityUser<Guid>> users, IdentityUser<Guid> user) =>
        new(user.Id, user.Email!, [.. await users.GetRolesAsync(user)]);

    private static ProblemHttpResult RegistrationFailed(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return TypedResults.Problem(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = ProblemTypes.Conflict,
                Title = "The request conflicts with the current state.",
                Detail = "An account with this email already exists.",
            });
        }

        var errors = result.Errors
            .GroupBy(FieldOf, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);

        return TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Type = ProblemTypes.UnprocessableEntity,
            Title = "A business rule was violated.",
            Detail = "The account could not be created.",
        });
    }

    // Identity error codes: PasswordTooShort, PasswordRequiresDigit, ..., InvalidEmail, InvalidUserName.
    private static string FieldOf(IdentityError error) =>
        error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password"
        : error.Code.Contains("Email", StringComparison.Ordinal) || error.Code.Contains("UserName", StringComparison.Ordinal) ? "email"
        : ApiExceptionHandler.GeneralErrorKey;
}

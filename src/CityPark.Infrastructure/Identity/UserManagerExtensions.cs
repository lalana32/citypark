using CityPark.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CityPark.Infrastructure.Identity;

public static class UserManagerExtensions
{
    /// <summary>
    /// Creates a user whose user name is the email and gives them <paramref name="role"/>, in one
    /// transaction, so a failure cannot leave an account without a role.
    /// </summary>
    public static Task<(IdentityUser<Guid> User, IdentityResult Result)> CreateWithRoleAsync(
        this UserManager<IdentityUser<Guid>> users,
        AppDbContext db,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(db);

        // UserManager saves through the same scoped AppDbContext, so it joins this transaction.
        // The retry strategy (ADR-002) replays the whole block, so it starts from an empty change tracker.
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var user = new IdentityUser<Guid> { Id = Guid.CreateVersion7(), UserName = email, Email = email };

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var result = await users.CreateAsync(user, password);
            if (result.Succeeded)
            {
                result = await users.AddToRoleAsync(user, role);
            }

            if (result.Succeeded)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return (user, result);
        });
    }
}

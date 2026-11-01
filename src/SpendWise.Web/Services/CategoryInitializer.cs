using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Data;
using SpendWise.Web.Models;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public sealed class CategoryInitializer(IDbContextFactory<ApplicationDbContext> factory, ICurrentUser currentUser)
{
    private static readonly string[] Defaults =
        ["Groceries", "Dining", "Transport", "Housing", "Utilities", "Health", "Entertainment", "Other"];

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            throw new UnauthorizedAccessException("Account unavailable.");
        if (await db.Users.AnyAsync(u => u.Id == userId && u.CategoriesInitialized, cancellationToken))
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Claim the one-time operation with a conditional write BEFORE reading categories.
        // SQLite serialises writers; a competing visit rechecks this flag after the winner commits.
        var claimed = await db.Users.Where(u => u.Id == userId && !u.CategoriesInitialized)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CategoriesInitialized, true), cancellationToken);
        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var existing = await db.Categories.Where(c => c.UserId == userId)
            .Select(c => c.NormalizedName).ToListAsync(cancellationToken);
        foreach (var name in Defaults)
            if (!existing.Contains(name.ToUpperInvariant()))
                db.Categories.Add(new Category { UserId = userId, Name = name });

        // Marker and categories commit together. Failure rolls both back, permitting a retry.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

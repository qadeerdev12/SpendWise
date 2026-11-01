using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Security;
using SpendWise.Web.Models;

namespace SpendWise.Web.Services;

public sealed class AccountDeletionService(IServiceScopeFactory scopes, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<ServiceResult<bool>> DeleteAsync(string password, bool confirmed, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        if (!confirmed) return ServiceResult<bool>.Invalid("Confirm that you want to permanently delete your account and data.");

        // Identity and our dependent-data deletes share ONE context and transaction in a
        // fresh scope. This avoids deleting expenses if Identity later rejects deletion.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // BEGIN: all database writes below are one all-or-nothing operation. "await using"
        // disposes this transaction even on an early return or exception. If we never
        // commit, disposal rolls back its writes; it does not undo a successful commit.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await users.FindByIdAsync(userId);
        if (user is null) return ServiceResult<bool>.NotFound();
        if (await users.IsLockedOutAsync(user) || !await users.HasPasswordAsync(user) ||
            !await users.CheckPasswordAsync(user, password ?? string.Empty))
            return ServiceResult<bool>.Invalid("Your current password could not be verified.");

        await db.Expenses.Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Categories.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded) return ServiceResult<bool>.Invalid("Your account could not be deleted. No data was removed.");
        // Add only queues the record in EF's change tracker. SaveChangesAsync sends its
        // INSERT to SQLite, but the outer transaction still has not committed anything.
        db.AccountDeletionRecords.Add(new AccountDeletionRecord
        {
            UserId = userId,
            DeletedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);

        // COMMIT: make ALL deletions and the record permanent together. An INSERT failure
        // above exits before this point and rolls back even Identity's earlier SQL DELETE.
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }
}

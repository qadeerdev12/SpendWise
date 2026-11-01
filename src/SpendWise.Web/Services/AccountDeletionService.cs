using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public sealed class AccountDeletionService(IServiceScopeFactory scopes, ICurrentUser currentUser)
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
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }
}

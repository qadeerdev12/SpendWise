using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Models;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public sealed class CategoryService(IDbContextFactory<ApplicationDbContext> factory, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<CategoryRow>> ListAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Categories.AsNoTracking().Where(c => c.UserId == userId && (includeArchived || !c.IsArchived))
            .OrderBy(c => c.Name).Select(c => new CategoryRow(c.Id, c.Name, c.IsArchived)).ToListAsync(ct);
    }

    public async Task<ServiceResult<int>> CreateAsync(string name, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        if (!ValidName(name)) return ServiceResult<int>.Invalid("Use a category name of 1–40 characters.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var category = new Category { UserId = userId, Name = name };
        db.Categories.Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        { return ServiceResult<int>.Invalid("That category name already exists, including archived categories."); }
        return ServiceResult<int>.Success(category.Id);
    }

    public async Task<ServiceResult<bool>> RenameAsync(int id, string name, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        // Look up ownership first so an inaccessible record always has the same outcome.
        if (category is null) return ServiceResult<bool>.NotFound();
        if (!ValidName(name)) return ServiceResult<bool>.Invalid("Use a category name of 1–40 characters.");
        category.Name = name;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        { return ServiceResult<bool>.Invalid("That category name already exists, including archived categories."); }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> SetArchivedAsync(int id, bool archived, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (category is null) return ServiceResult<bool>.NotFound();
        category.IsArchived = archived;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name)
        && name.Trim().Length <= 40 && name.Trim().ToUpperInvariant().Length <= 40;

    // Translate only SQLite's unique-constraint violation; infrastructure failures must not
    // be misreported as user validation errors. The index also covers concurrent requests.
    private static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };
}

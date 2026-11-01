using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Models;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public sealed class ExpenseService(IDbContextFactory<ApplicationDbContext> factory, ICurrentUser currentUser,
    TimeProvider clock)
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public async Task<ServiceResult<ExpensePage>> ListAsync(ExpenseFilter filter, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var today = Today();
        if (filter.Year < 2000 || filter.Year > today.Year || filter.Month is < 1 or > 12 ||
            (filter.Year == today.Year && filter.Month > today.Month) || filter.Page < 1 || filter.Page > int.MaxValue / 20)
            return ServiceResult<ExpensePage>.Invalid("Choose a supported month and page.");
        var start = new DateOnly(filter.Year, filter.Month, 1);
        var end = start.AddMonths(1);
        await using var db = await factory.CreateDbContextAsync(ct);
        // Owner scoping is applied BEFORE filters, counts or pagination.
        var query = db.Expenses.AsNoTracking().Where(e => e.UserId == userId && e.ExpenseDate >= start && e.ExpenseDate < end);
        if (filter.CategoryId is int categoryId) query = query.Where(e => e.CategoryId == categoryId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * 20).Take(20)
            .Select(e => new ExpenseRow(e.Id, e.AmountCents, e.ExpenseDate, e.CategoryId, e.Category.Name, e.Merchant, e.Notes))
            .ToListAsync(ct);
        return ServiceResult<ExpensePage>.Success(new ExpensePage(rows, count, filter.Page, 20));
    }

    public async Task<ServiceResult<ExpenseRow>> GetAsync(int id, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Expenses.AsNoTracking().Where(e => e.Id == id && e.UserId == userId)
            .Select(e => new ExpenseRow(e.Id, e.AmountCents, e.ExpenseDate, e.CategoryId, e.Category.Name, e.Merchant, e.Notes))
            .SingleOrDefaultAsync(ct);
        return row is null ? ServiceResult<ExpenseRow>.NotFound() : ServiceResult<ExpenseRow>.Success(row);
    }

    public async Task<ServiceResult<int>> CreateAsync(ExpenseInput input, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var error = Validate(input);
        if (error is not null) return ServiceResult<int>.Invalid(error);
        await using var db = await factory.CreateDbContextAsync(ct);
        // Keep category validation and the write in one transaction so an archive cannot
        // slip between them and allow a new assignment to an archived category.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == userId && !c.IsArchived, ct))
            return ServiceResult<int>.Invalid("Choose an available category.");
        var expense = new Expense { UserId = userId, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        Apply(expense, input);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<int>.Success(expense.Id);
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, ExpenseInput input, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (expense is null) return ServiceResult<bool>.NotFound();
        var error = Validate(input);
        if (error is not null) return ServiceResult<bool>.Invalid(error);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == input.CategoryId && c.UserId == userId, ct);
        // Existing history may retain an archived category, but cannot switch into one.
        if (category is null || (category.IsArchived && expense.CategoryId != category.Id))
            return ServiceResult<bool>.Invalid("Choose an available category.");
        Apply(expense, input);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        await using var db = await factory.CreateDbContextAsync(ct);
        var removed = await db.Expenses.Where(e => e.Id == id && e.UserId == userId).ExecuteDeleteAsync(ct);
        return removed == 0 ? ServiceResult<bool>.NotFound() : ServiceResult<bool>.Success(true);
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Sydney).DateTime);
    private string? Validate(ExpenseInput input)
    {
        if (input.AmountCents is < 1 or > 99_999_999) return "Amount must be A$0.01–A$999,999.99.";
        if (input.ExpenseDate < new DateOnly(2000, 1, 1) || input.ExpenseDate > Today()) return "Choose a date from 1 January 2000 through today in Sydney.";
        if (input.Merchant?.Trim().Length > 100) return "Merchant must be at most 100 characters.";
        if (input.Notes?.Trim().Length > 500) return "Notes must be at most 500 characters.";
        return null;
    }

    private void Apply(Expense expense, ExpenseInput input)
    {
        expense.AmountCents = input.AmountCents;
        expense.ExpenseDate = input.ExpenseDate;
        expense.CategoryId = input.CategoryId;
        expense.Merchant = string.IsNullOrWhiteSpace(input.Merchant) ? null : input.Merchant.Trim();
        expense.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        expense.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
    }
}

using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Security;

namespace SpendWise.Web.Services;

public sealed class DashboardService(IDbContextFactory<ApplicationDbContext> factory,
    ICurrentUser currentUser, TimeProvider clock)
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    public async Task<ServiceResult<DashboardSummary>> GetMonthlySummaryAsync(int year, int month,
        CancellationToken ct = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Sydney).DateTime);
        if (year < 2000 || year > today.Year || month is < 1 or > 12 ||
            (year == today.Year && month > today.Month))
            return ServiceResult<DashboardSummary>.Invalid("Choose a month from January 2000 through the current month in Sydney.");

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        await using var db = await factory.CreateDbContextAsync(ct);
        // Scope to the authenticated owner BEFORE aggregation. Do not reuse the paged
        // ledger query: this summary includes every expense and archived-category history.
        var categories = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= start && e.ExpenseDate < end)
            .GroupBy(e => new { e.CategoryId, e.Category.Name, e.Category.IsArchived })
            .Select(g => new CategorySpending(g.Key.CategoryId, g.Key.Name, g.Key.IsArchived,
                g.Sum(e => e.AmountCents), g.Count()))
            .ToListAsync(ct);
        // Derive cards from the same query result so cards and category rows agree,
        // even if another request writes an expense while this page is loading.
        var ordered = categories.OrderByDescending(c => c.TotalCents)
            .ThenBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.CategoryId).ToArray();
        return ServiceResult<DashboardSummary>.Success(new(categories.Sum(c => c.TotalCents),
            categories.Sum(c => c.ExpenseCount), ordered));
    }
}

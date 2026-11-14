namespace SpendWise.Web.Contracts;

// Reporting values stay in integer cents until the page formats them for display.
public sealed record CategorySpending(int CategoryId, string CategoryName, bool IsArchived,
    long TotalCents, int ExpenseCount);
public sealed record DashboardSummary(long TotalCents, int ExpenseCount,
    IReadOnlyList<CategorySpending> Categories);

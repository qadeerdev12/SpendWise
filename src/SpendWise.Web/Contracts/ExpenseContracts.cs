namespace SpendWise.Web.Contracts;

// Owner IDs and audit timestamps are deliberately absent from caller-supplied input.
public sealed record ExpenseInput(long AmountCents, DateOnly ExpenseDate, int CategoryId,
    string? Merchant = null, string? Notes = null);
public sealed record ExpenseRow(int Id, long AmountCents, DateOnly ExpenseDate, int CategoryId,
    string CategoryName, string? Merchant, string? Notes);
public sealed record ExpenseFilter(int Year, int Month, int? CategoryId = null, int Page = 1);
public sealed record ExpensePage(IReadOnlyList<ExpenseRow> Items, int TotalCount, int Page, int PageSize);
public sealed record CategoryRow(int Id, string Name, bool IsArchived);

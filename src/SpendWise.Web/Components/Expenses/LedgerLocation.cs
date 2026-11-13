using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace SpendWise.Web.Components.Expenses;

// Only validated filter values travel between pages, never an arbitrary return URL.
public sealed record LedgerLocation(string Month, int CategoryId = 0, int Page = 1)
{
    public static LedgerLocation FromQuery(string? month, string? category, string? page, TimeProvider clock)
    {
        var today = ExpenseFormModel.Today(clock);
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date < new DateOnly(2000, 1, 1) || date > today)
            month = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var categoryId = int.TryParse(category, out var c) && c > 0 ? c : 0;
        var pageNumber = int.TryParse(page, out var p) && p > 0 && p <= int.MaxValue / 20 ? p : 1;
        return new(month!, categoryId, pageNumber);
    }

    public string Url(string path = "expenses") => QueryHelpers.AddQueryString(path,
        new Dictionary<string, string?>
        {
            ["month"] = Month,
            ["categoryId"] = CategoryId.ToString(CultureInfo.InvariantCulture),
            ["page"] = Page.ToString(CultureInfo.InvariantCulture)
        });
}

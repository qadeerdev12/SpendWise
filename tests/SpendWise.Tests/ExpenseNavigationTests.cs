using Microsoft.AspNetCore.WebUtilities;
using SpendWise.Web.Components.Expenses;
using SpendWise.Web.Contracts;

namespace SpendWise.Tests;

public sealed class ExpenseNavigationTests
{
    [Fact]
    public void ValidReturnFilters_RoundTripThroughFixedLocalUrl()
    {
        var location = LedgerLocation.FromQuery("2026-10", "3", "2", new FixedClock());
        var url = location.Url();
        Assert.StartsWith("expenses?", url);
        var query = QueryHelpers.ParseQuery(url[url.IndexOf('?')..]);
        Assert.Equal("2026-10", query["month"].ToString());
        Assert.Equal("3", query["categoryId"].ToString());
        Assert.Equal("2", query["page"].ToString());
        Assert.StartsWith("expenses/42/edit?", location.Url("expenses/42/edit"));
        Assert.StartsWith("expenses/42/delete?", location.Url("expenses/42/delete"));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("bad", "bad", "bad")]
    [InlineData("https://example.invalid", "-1", "0")]
    [InlineData("1999-12", "0", "2147483647")]
    [InlineData("2026-12", "999999999999", "-1")]
    public void InvalidQueryValues_FallBackToSafeSydneyDefaults(string? month, string? category, string? page)
    {
        Assert.Equal(new LedgerLocation("2026-11", 0, 1), LedgerLocation.FromQuery(month, category, page, new FixedClock()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(99999999)]
    public void EditPrefill_RoundTripsAllValuesWithoutRounding(long cents)
    {
        var row = new ExpenseRow(42, cents, new(2026,10,31), 3, "Groceries", "Shop", "Notes");
        var model = ExpenseFormModel.FromExpense(row, new FixedClock());
        Assert.Equal(new ExpenseInput(cents, row.ExpenseDate, 3, "Shop", "Notes"), model.ToInput());
    }

    [Fact]
    public void SuccessFeedback_IsConsumedOnce()
    {
        var feedback = new ExpenseFeedback { Message = "Expense updated." };
        Assert.Equal("Expense updated.", feedback.Take());
        Assert.Null(feedback.Take());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,11,1,13,30,0,TimeSpan.Zero);
    }
}

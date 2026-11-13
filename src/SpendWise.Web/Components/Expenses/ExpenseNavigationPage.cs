using Microsoft.AspNetCore.Components;

namespace SpendWise.Web.Components.Expenses;

// The three pages share query binding, not business logic or database access.
public abstract class ExpenseNavigationPage : ComponentBase
{
    [Inject] protected TimeProvider Clock { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "month")] public string? MonthQuery { get; set; }
    [SupplyParameterFromQuery(Name = "categoryId")] public string? CategoryQuery { get; set; }
    [SupplyParameterFromQuery(Name = "page")] public string? PageQuery { get; set; }
    protected LedgerLocation ReturnLocation => LedgerLocation.FromQuery(MonthQuery, CategoryQuery, PageQuery, Clock);
}

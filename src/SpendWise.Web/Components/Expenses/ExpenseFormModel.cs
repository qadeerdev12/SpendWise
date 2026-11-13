using System.ComponentModel.DataAnnotations;
using SpendWise.Web.Contracts;

namespace SpendWise.Web.Components.Expenses;

// UI values stay in dollars until validation succeeds; persistence accepts integer cents.
public sealed class ExpenseFormModel(TimeProvider clock) : IValidatableObject
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
    [Required(ErrorMessage = "Enter an amount in AUD.")]
    public decimal? Amount { get; set; }
    [Required(ErrorMessage = "Choose an expense date.")]
    public DateOnly? ExpenseDate { get; set; } = Today(clock);
    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }
    public string? Merchant { get; set; }
    public string? Notes { get; set; }

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Sydney).DateTime);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount is decimal amount)
        {
            if (amount is < 0.01m or > 999999.99m)
                yield return new("Enter an amount from A$0.01 to A$999,999.99.", [nameof(Amount)]);
            else if (amount * 100m != decimal.Truncate(amount * 100m))
                yield return new("Use no more than two decimal places. Amounts are not rounded.", [nameof(Amount)]);
        }
        if (ExpenseDate is DateOnly date && (date < new DateOnly(2000, 1, 1) || date > Today(clock)))
            yield return new("Choose a date from 1 January 2000 through today in Sydney.", [nameof(ExpenseDate)]);
        if (Merchant?.Trim().Length > 100)
            yield return new("Merchant must be at most 100 characters.", [nameof(Merchant)]);
        if (Notes?.Trim().Length > 500)
            yield return new("Notes must be at most 500 characters.", [nameof(Notes)]);
    }

    public static ExpenseFormModel FromExpense(ExpenseRow expense, TimeProvider clock) => new(clock)
    {
        // Integer cents become decimal dollars exactly; both create and edit share validation.
        Amount = expense.AmountCents / 100m,
        ExpenseDate = expense.ExpenseDate,
        CategoryId = expense.CategoryId,
        Merchant = expense.Merchant,
        Notes = expense.Notes
    };

    public ExpenseInput ToInput()
    {
        // Defend the conversion boundary even if a future caller skips form validation.
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        return new(checked((long)(Amount!.Value * 100m)), ExpenseDate!.Value, CategoryId, Merchant, Notes);
    }
}

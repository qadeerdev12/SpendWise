namespace SpendWise.Web.Models;

/// <summary>A single manual AUD expense belonging to one account.</summary>
public class Expense
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Whole cents preserve exact money values: A$24.50 is stored as 2450.
    public long AmountCents { get; set; }

    // A purchase date is a calendar day, not a UTC timestamp.
    public DateOnly ExpenseDate { get; set; }
    public string? Merchant { get; set; }
    public string? Notes { get; set; }

    // The application service will assign UTC audit values when it saves a record.
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

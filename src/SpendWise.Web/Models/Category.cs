namespace SpendWise.Web.Models;

/// <summary>A user-owned label that can be archived without losing expense history.</summary>
public class Category
{
    private string _name = string.Empty;

    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            // Keep the display name and uniqueness key together, including on rename.
            _name = (value ?? string.Empty).Trim();
            NormalizedName = _name.ToUpperInvariant();
        }
    }

    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsArchived { get; set; }
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

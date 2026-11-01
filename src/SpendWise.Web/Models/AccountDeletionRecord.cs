namespace SpendWise.Web.Models;

// Minimal deletion evidence, not a copy of the user's personal or financial data.
public sealed class AccountDeletionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // Deliberately no navigation/FK to Identity: this ID must survive account deletion.
    public string UserId { get; set; } = string.Empty;
    public DateTime DeletedAtUtc { get; set; }
    // A future exporter sets this only after independent storage acknowledges delivery.
    public DateTime? ExportedAtUtc { get; set; }
}

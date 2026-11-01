using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Web.Models;

namespace SpendWise.Web.Data.Configurations;

public sealed class AccountDeletionRecordConfiguration : IEntityTypeConfiguration<AccountDeletionRecord>
{
    public void Configure(EntityTypeBuilder<AccountDeletionRecord> builder)
    {
        builder.ToTable("AccountDeletionRecords", table =>
            table.HasCheckConstraint("CK_AccountDeletionRecords_UserId", "length(trim(UserId)) > 0"));
        builder.HasKey(record => record.Id);
        builder.Property(record => record.UserId).IsRequired();
        // One successful deletion per internal account ID. Re-registering creates a new ID.
        builder.HasIndex(record => record.UserId).IsUnique();
        // No relationship to AspNetUsers: deletion evidence is independent of the live account.
    }
}

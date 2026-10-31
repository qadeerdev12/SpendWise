using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Web.Models;

namespace SpendWise.Web.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", table =>
        {
            table.HasCheckConstraint("CK_Expenses_AmountCents", "AmountCents BETWEEN 1 AND 99999999");
            table.HasCheckConstraint("CK_Expenses_MerchantLength", "Merchant IS NULL OR length(Merchant) <= 100");
            table.HasCheckConstraint("CK_Expenses_NotesLength", "Notes IS NULL OR length(Notes) <= 500");
        });
        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.UserId).IsRequired();
        builder.Property(expense => expense.AmountCents).HasColumnType("INTEGER");
        builder.Property(expense => expense.ExpenseDate).IsRequired();
        builder.Property(expense => expense.Merchant).HasMaxLength(100);
        builder.Property(expense => expense.Notes).HasMaxLength(500);
        builder.Property(expense => expense.CreatedAtUtc).IsRequired();
        builder.Property(expense => expense.UpdatedAtUtc).IsRequired();

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(expense => expense.UserId).OnDelete(DeleteBehavior.Restrict);

        // Matching both fields prevents assigning another user's category, even via raw SQL.
        builder.HasOne(expense => expense.Category).WithMany(category => category.Expenses)
            .HasForeignKey(expense => new { expense.CategoryId, expense.UserId })
            .HasPrincipalKey(category => new { category.Id, category.UserId })
            .OnDelete(DeleteBehavior.Restrict);

        // Lead with the owner because every future ledger query must be user-scoped.
        builder.HasIndex(expense => new { expense.UserId, expense.ExpenseDate, expense.Id });
        builder.HasIndex(expense => new { expense.UserId, expense.CategoryId, expense.ExpenseDate });
    }
}

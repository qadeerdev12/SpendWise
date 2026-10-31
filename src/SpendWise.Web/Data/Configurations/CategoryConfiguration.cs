using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Web.Models;

namespace SpendWise.Web.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table =>
        {
            // SQLite does not enforce HasMaxLength, so protect stored lengths explicitly.
            table.HasCheckConstraint("CK_Categories_Name", "length(trim(Name)) BETWEEN 1 AND 40");
            table.HasCheckConstraint("CK_Categories_NormalizedName", "length(NormalizedName) BETWEEN 1 AND 40");
        });
        builder.HasKey(category => category.Id);
        builder.Property(category => category.UserId).IsRequired();
        builder.Property(category => category.Name).IsRequired().HasMaxLength(40);
        builder.Property(category => category.NormalizedName).IsRequired().HasMaxLength(40);
        builder.Property(category => category.IsArchived).HasDefaultValue(false);

        // Archived names stay reserved, preventing duplicate histories on restore.
        builder.HasIndex(category => new { category.UserId, category.NormalizedName }).IsUnique();
        builder.HasAlternateKey(category => new { category.Id, category.UserId });
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(category => category.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

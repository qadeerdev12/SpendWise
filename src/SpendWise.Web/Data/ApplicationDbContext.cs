using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpendWise.Web.Data.Configurations;
using SpendWise.Web.Models;

namespace SpendWise.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<AccountDeletionRecord> AccountDeletionRecords => Set<AccountDeletionRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Preserve Identity's mappings before adding our application's relationships.
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new ExpenseConfiguration());
        builder.ApplyConfiguration(new AccountDeletionRecordConfiguration());
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Web.Data;
using SpendWise.Web.Models;

namespace SpendWise.Tests.Integration;

// A real SQLite database exercises the constraints that EF's InMemory provider cannot enforce.
public class ExpenseCategoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>(options =>
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        _provider = services.BuildServiceProvider();
        await UsingDatabase(async db =>
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(new ApplicationUser { Id = "owner-a", UserName = "a@example.invalid" },
                new ApplicationUser { Id = "owner-b", UserName = "b@example.invalid" });
            db.Categories.Add(new Category { UserId = "owner-a", Name = "Groceries" });
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // Each operation uses a fresh context, preventing tracked entities from hiding storage errors.
    private async Task UsingDatabase(Func<ApplicationDbContext, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static Expense Purchase(int categoryId, long cents = 2450, string owner = "owner-a") => new()
    {
        UserId = owner, CategoryId = categoryId, AmountCents = cents,
        ExpenseDate = new DateOnly(2026, 10, 3), Merchant = "Test shop",
        CreatedAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)
    };

    [Theory]
    [InlineData(1L)]
    [InlineData(2450L)]
    [InlineData(99999999L)]
    public async Task ValidExpense_RoundTripsExactCentsAndDate(long cents)
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(await db.Categories.Select(c => c.Id).SingleAsync(), cents));
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            var saved = await db.Expenses.Include(e => e.Category).SingleAsync();
            Assert.Equal(cents, saved.AmountCents);
            Assert.Equal(new DateOnly(2026, 10, 3), saved.ExpenseDate);
            Assert.Equal("Groceries", saved.Category.Name);
        });
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(100000000L)]
    public async Task InvalidAmount_IsRejectedByDatabase(long cents)
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(await db.Categories.Select(c => c.Id).SingleAsync(), cents));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await UsingDatabase(async db => Assert.Empty(await db.Expenses.ToListAsync()));
    }

    [Fact]
    public async Task AnotherOwnersCategory_IsRejected()
    {
        await UsingDatabase(async db =>
        {
            // Both accounts exist, so rejection proves the composite ownership FK works.
            var categoryId = await db.Categories.Select(c => c.Id).SingleAsync();
            db.Expenses.Add(Purchase(categoryId, owner: "owner-b"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await UsingDatabase(async db => Assert.Empty(await db.Expenses.ToListAsync()));
    }

    [Fact]
    public async Task ChangingExpenseOwnerWithoutCategory_IsRejected()
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(await db.Categories.Select(c => c.Id).SingleAsync()));
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            var expense = await db.Expenses.SingleAsync();
            expense.UserId = "owner-b";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await UsingDatabase(async db => Assert.Equal("owner-a", (await db.Expenses.SingleAsync()).UserId));
    }

    [Fact]
    public async Task MissingCategory_IsRejected()
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(int.MaxValue));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateCategoryName_IsRejectedIncludingArchivedNames(bool archiveOriginal)
    {
        await UsingDatabase(async db =>
        {
            (await db.Categories.SingleAsync()).IsArchived = archiveOriginal;
            await db.SaveChangesAsync();
            db.Categories.Add(new Category { UserId = "owner-a", Name = "  gRoCeRiEs  " });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task DifferentOwners_CanUseTheSameCategoryName()
    {
        await UsingDatabase(async db =>
        {
            db.Categories.Add(new Category { UserId = "owner-b", Name = "groceries" });
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db => Assert.Equal(2, await db.Categories.CountAsync()));
    }

    [Fact]
    public async Task Rename_UpdatesDisplayAndNormalizedName()
    {
        await UsingDatabase(async db =>
        {
            (await db.Categories.SingleAsync()).Name = "  Food shopping  ";
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            var category = await db.Categories.SingleAsync();
            Assert.Equal("Food shopping", category.Name);
            Assert.Equal("FOOD SHOPPING", category.NormalizedName);
        });
    }

    [Fact]
    public async Task RenameToExistingName_IsRejected()
    {
        await UsingDatabase(async db =>
        {
            db.Categories.Add(new Category { UserId = "owner-a", Name = "Dining" });
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            (await db.Categories.SingleAsync(c => c.Name == "Dining")).Name = "groceries";
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task ArchivingCategory_PreservesExpenseHistory()
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(await db.Categories.Select(c => c.Id).SingleAsync()));
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            (await db.Categories.SingleAsync()).IsArchived = true;
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            var saved = await db.Expenses.Include(e => e.Category).SingleAsync();
            Assert.True(saved.Category.IsArchived);
            Assert.Equal(2450, saved.AmountCents);
        });
    }

    [Fact]
    public async Task ReferencedCategory_CannotBeDeleted()
    {
        await UsingDatabase(async db =>
        {
            db.Expenses.Add(Purchase(await db.Categories.Select(c => c.Id).SingleAsync()));
            await db.SaveChangesAsync();
        });
        await UsingDatabase(async db =>
        {
            // Do not load dependents: the database itself must reject this deletion.
            db.Categories.Remove(await db.Categories.SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await UsingDatabase(async db =>
        {
            Assert.Equal(1, await db.Categories.CountAsync());
            Assert.Equal(1, await db.Expenses.CountAsync());
        });
    }

    [Fact]
    public async Task OwnerWithCategory_CannotBeDeleted()
    {
        await UsingDatabase(async db =>
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == "owner-a"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public async Task InvalidCategoryNameLength_IsRejected(int length)
    {
        await UsingDatabase(async db =>
        {
            db.Categories.Add(new Category { UserId = "owner-a", Name = new string('x', length) });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Theory]
    [InlineData(101, 0)]
    [InlineData(0, 501)]
    public async Task OversizedOptionalText_IsRejected(int merchantLength, int notesLength)
    {
        await UsingDatabase(async db =>
        {
            var expense = Purchase(await db.Categories.Select(c => c.Id).SingleAsync());
            expense.Merchant = new string('x', merchantLength);
            expense.Notes = new string('x', notesLength);
            db.Expenses.Add(expense);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Services;

namespace SpendWise.Tests.Integration;

// File-backed SQLite uses independent connections, including for concurrent first visits.
public sealed partial class UserServicesTests(Xunit.Abstractions.ITestOutputHelper output) : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"spendwise-services-{Guid.NewGuid():N}.db");
    private readonly string _password = $"Test!{Guid.NewGuid():N}aA7";
    private ServiceProvider _provider = null!;
    private static readonly DateOnly Today = new(2026, 11, 2);

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpendWiseServices($"Data Source={_path};Foreign Keys=True;Pooling=False;Default Timeout=10");
        services.AddScoped<AuthenticationStateProvider, TestAuthentication>();
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            options.Password.RequiredLength = 12;
        }).AddEntityFrameworkStores<ApplicationDbContext>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var id in new[] { "alice", "bob" })
            Assert.True((await users.CreateAsync(new ApplicationUser { Id = id, UserName = $"{id}@example.invalid", Email = $"{id}@example.invalid" }, _password)).Succeeded);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
    }

    private async Task<T> As<T>(string? userId, Func<IServiceProvider, Task<T>> operation)
    {
        await using var scope = _provider.CreateAsyncScope();
        ((TestAuthentication)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>()).UserId = userId;
        return await operation(scope.ServiceProvider);
    }

    private Task Initialize(string user) => As(user, async sp =>
    {
        await sp.GetRequiredService<CategoryInitializer>().EnsureInitializedAsync();
        return true;
    });

    private Task<IReadOnlyList<CategoryRow>> Categories(string user) =>
        As(user, sp => sp.GetRequiredService<CategoryService>().ListAsync(true));

    private async Task<int> AddExpense(string user, long cents = 2450)
    {
        await Initialize(user);
        var category = (await Categories(user))[0];
        var result = await As(user, sp => sp.GetRequiredService<ExpenseService>()
            .CreateAsync(new ExpenseInput(cents, Today, category.Id, "  Shop  ", "  ")));
        Assert.Equal(ResultStatus.Success, result.Status);
        return result.Value;
    }

    [Fact]
    public async Task SignedOut_AllServiceEntryPointsRejectAccess()
    {
        await As<object?>(null, async sp =>
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026, 11));
            var expenses = sp.GetRequiredService<ExpenseService>();
            var categories = sp.GetRequiredService<CategoryService>();
            var input = new ExpenseInput(100, Today, 1);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expenses.ListAsync(new(2026, 11)));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expenses.GetAsync(1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expenses.CreateAsync(input));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expenses.UpdateAsync(1, input));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => expenses.DeleteAsync(1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => categories.ListAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => categories.CreateAsync("Food"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => categories.RenameAsync(1, "Food"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => categories.SetArchivedAsync(1, true));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sp.GetRequiredService<CategoryInitializer>().EnsureInitializedAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true));
            return null;
        });
    }

    [Fact]
    public async Task Initialization_IsPerUserAndIdempotent()
    {
        await Initialize("alice");
        await Initialize("alice");
        Assert.Equal(8, (await Categories("alice")).Count);
        Assert.Empty(await Categories("bob"));
        await Initialize("bob");
        Assert.Equal(8, (await Categories("bob")).Count);
        Assert.Empty((await Categories("alice")).Select(c => c.Id).Intersect((await Categories("bob")).Select(c => c.Id)));
    }

    [Fact]
    public async Task ConcurrentInitialization_CreatesExactlyEightCategories()
    {
        // Separate requests/scopes/connections compete for the same durable marker.
        await Task.WhenAll(Task.Run(() => Initialize("alice")), Task.Run(() => Initialize("alice")));
        Assert.Equal(8, (await Categories("alice")).Count);
    }

    [Fact]
    public async Task RenamedAndArchivedDefaults_AreNotRecreated()
    {
        await Initialize("alice");
        var category = (await Categories("alice")).Single(c => c.Name == "Groceries");
        await As("alice", sp => sp.GetRequiredService<CategoryService>().RenameAsync(category.Id, "Food shopping"));
        await As("alice", sp => sp.GetRequiredService<CategoryService>().SetArchivedAsync(category.Id, true));
        await Initialize("alice");
        var all = await Categories("alice");
        Assert.Equal(8, all.Count);
        Assert.DoesNotContain(all, c => c.Name == "Groceries");
        Assert.True(all.Single(c => c.Id == category.Id).IsArchived);
    }

    [Fact]
    public async Task InitializationFailure_RollsBackMarkerAndPartialCategories()
    {
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            // A database fault mid-seed proves the transaction restores both marker and rows.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailSeed BEFORE INSERT ON Categories WHEN NEW.Name = 'Transport' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
            await Assert.ThrowsAsync<DbUpdateException>(() => sp.GetRequiredService<CategoryInitializer>().EnsureInitializedAsync());
            Assert.False(await db.Users.Where(u => u.Id == "alice").Select(u => u.CategoriesInitialized).SingleAsync());
            Assert.Empty(await db.Categories.ToListAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailSeed;");
            return true;
        });
        await Initialize("alice");
        Assert.Equal(8, (await Categories("alice")).Count);
    }

    [Fact]
    public async Task ForeignIds_CannotBeReadChangedOrDeleted()
    {
        var id = await AddExpense("alice");
        await Initialize("bob");
        var aliceCategory = (await Categories("alice"))[0].Id;
        var bobCategory = (await Categories("bob"))[0].Id;
        await As("bob", async sp =>
        {
            var expenses = sp.GetRequiredService<ExpenseService>();
            Assert.Empty((await expenses.ListAsync(new(2026, 11))).Value!.Items);
            Assert.Equal(ResultStatus.NotFound, (await expenses.GetAsync(id)).Status);
            Assert.Equal(ResultStatus.NotFound, (await expenses.UpdateAsync(id, new(999, Today, bobCategory))).Status);
            Assert.Equal(ResultStatus.NotFound, (await expenses.DeleteAsync(id)).Status);
            Assert.Equal(ResultStatus.ValidationFailure, (await expenses.CreateAsync(new(999, Today, aliceCategory))).Status);
            var categories = sp.GetRequiredService<CategoryService>();
            Assert.Equal(ResultStatus.NotFound, (await categories.RenameAsync(aliceCategory, "Stolen")).Status);
            Assert.Equal(ResultStatus.NotFound, (await categories.SetArchivedAsync(aliceCategory, true)).Status);
            return true;
        });
        var original = await As("alice", sp => sp.GetRequiredService<ExpenseService>().GetAsync(id));
        Assert.Equal(2450, original.Value!.AmountCents);
        Assert.False((await Categories("alice")).Single(c => c.Id == aliceCategory).IsArchived);
    }

    [Fact]
    public async Task OwnCrud_WorksAndAssignsProtectedFields()
    {
        var id = await AddExpense("alice");
        var row = await As("alice", sp => sp.GetRequiredService<ExpenseService>().GetAsync(id));
        Assert.Equal("Shop", row.Value!.Merchant);
        Assert.Null(row.Value.Notes);
        var changed = await As("alice", sp => sp.GetRequiredService<ExpenseService>().UpdateAsync(id, new(3000, Today, row.Value.CategoryId)));
        Assert.Equal(ResultStatus.Success, changed.Status);
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var expense = await db.Expenses.SingleAsync();
            Assert.Equal("alice", expense.UserId);
            Assert.Equal(3000, expense.AmountCents);
            Assert.Equal(new DateTime(2026, 11, 1, 13, 30, 0), expense.CreatedAtUtc);
            Assert.Equal(expense.CreatedAtUtc, expense.UpdatedAtUtc);
            return true;
        });
        Assert.Equal(ResultStatus.Success, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().DeleteAsync(id))).Status);
        Assert.Equal(ResultStatus.NotFound, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().DeleteAsync(id))).Status);
    }

    [Fact]
    public async Task ArchiveRules_AllowRetentionButRejectNewAssignments()
    {
        var id = await AddExpense("alice");
        var categories = await Categories("alice");
        var current = categories[0].Id;
        var other = categories[1].Id;
        await As("alice", sp => sp.GetRequiredService<CategoryService>().SetArchivedAsync(current, true));
        await As("alice", sp => sp.GetRequiredService<CategoryService>().SetArchivedAsync(other, true));
        await As("alice", async sp =>
        {
            var expenses = sp.GetRequiredService<ExpenseService>();
            Assert.Equal(ResultStatus.ValidationFailure, (await expenses.CreateAsync(new(100, Today, current))).Status);
            Assert.Equal(ResultStatus.Success, (await expenses.UpdateAsync(id, new(100, Today, current))).Status);
            Assert.Equal(ResultStatus.ValidationFailure, (await expenses.UpdateAsync(id, new(100, Today, other))).Status);
            return true;
        });
    }

    [Fact]
    public async Task DuplicateCategory_IsReportedAsValidation()
    {
        await Initialize("alice");
        var result = await As("alice", sp => sp.GetRequiredService<CategoryService>().CreateAsync("  groceries  "));
        Assert.Equal(ResultStatus.ValidationFailure, result.Status);
        Assert.Equal(8, (await Categories("alice")).Count);
    }

    [Fact]
    public async Task ServiceValidation_UsesSydneyTodayAndRejectsInvalidInput()
    {
        await Initialize("alice");
        var category = (await Categories("alice"))[0].Id;
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            // UTC is still 1 November; Sydney is already 2 November.
            Assert.Equal(ResultStatus.Success, (await service.CreateAsync(new(1, Today, category))).Status);
            foreach (var input in new[] {
                new ExpenseInput(0, Today, category), new ExpenseInput(100000000, Today, category),
                new ExpenseInput(1, Today.AddDays(1), category), new ExpenseInput(1, new(1999,12,31), category),
                new ExpenseInput(1, Today, category, new string('x',101)),
                new ExpenseInput(1, Today, category, Notes:new string('x',501)) })
                Assert.Equal(ResultStatus.ValidationFailure, (await service.CreateAsync(input)).Status);
            return true;
        });
    }

    [Fact]
    public async Task PagingAndCategoryFilter_StayOwnerScoped()
    {
        await Initialize("alice");
        await AddExpense("bob");
        var category = (await Categories("alice"))[0].Id;
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            for (var i = 0; i < 21; i++) await service.CreateAsync(new(100, Today, category));
            var first = (await service.ListAsync(new(2026,11))).Value!;
            var second = (await service.ListAsync(new(2026,11,Page:2))).Value!;
            Assert.Equal(21, first.TotalCount);
            Assert.Equal(20, first.Items.Count);
            Assert.Single(second.Items);
            Assert.True(first.Items[0].Id > second.Items[0].Id);
            Assert.Empty((await service.ListAsync(new(2026,10))).Value!.Items);
            Assert.Empty((await service.ListAsync(new(2026,11,CategoryId:int.MaxValue))).Value!.Items);
            return true;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletion_RequiresConfirmationAndCorrectPassword(bool confirmed)
    {
        await AddExpense("alice");
        var result = await As("alice", sp => sp.GetRequiredService<AccountDeletionService>()
            .DeleteAsync(confirmed ? "WrongPassword!" : _password, confirmed));
        Assert.Equal(ResultStatus.ValidationFailure, result.Status);
        await As("alice", async sp =>
        {
            Assert.Empty(await sp.GetRequiredService<ApplicationDbContext>().AccountDeletionRecords.ToListAsync());
            return true;
        });
        Assert.Equal(8, (await Categories("alice")).Count);
        Assert.Single((await As("alice", sp => sp.GetRequiredService<ExpenseService>().ListAsync(new(2026,11)))).Value!.Items);
    }

    [Fact]
    public async Task Deletion_RemovesOnlyCurrentUsersAccountAndData()
    {
        await AddExpense("alice");
        var bobExpense = await AddExpense("bob");
        Assert.Equal(ResultStatus.Success, (await As("alice", sp => sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true))).Status);
        await As("bob", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Users.AnyAsync(u => u.Id == "alice"));
            Assert.False(await db.Categories.AnyAsync(c => c.UserId == "alice"));
            Assert.False(await db.Expenses.AnyAsync(e => e.UserId == "alice"));
            Assert.True(await db.Users.AnyAsync(u => u.Id == "bob"));
            Assert.Equal(bobExpense, (await db.Expenses.SingleAsync()).Id);
            Assert.Equal(8, await db.Categories.CountAsync());
            var record = Assert.Single(await db.AccountDeletionRecords.ToListAsync());
            Assert.NotEqual(Guid.Empty, record.Id);
            Assert.Equal("alice", record.UserId);
            Assert.Equal(new DateTime(2026, 11, 1, 13, 30, 0), record.DeletedAtUtc);
            Assert.Null(record.ExportedAtUtc);
            return true;
        });
    }

    [Fact]
    public async Task DeletionFailure_RollsBackDependentData()
    {
        var id = await AddExpense("alice");
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            // Force failure at the final account delete after expenses/categories were removed.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailAccountDelete BEFORE DELETE ON AspNetUsers BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
            await Assert.ThrowsAsync<DbUpdateException>(() => sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true));
            Assert.Empty(await db.AccountDeletionRecords.ToListAsync());
            Assert.True(await db.Users.AnyAsync(u => u.Id == "alice"));
            return true;
        });
        Assert.Equal(8, (await Categories("alice")).Count);
        Assert.Equal(ResultStatus.Success, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().GetAsync(id))).Status);
    }

    [Fact]
    public async Task DeletionRecordFailure_RollsBackAccountAndData_ThenRetrySucceeds()
    {
        var aliceExpense = await AddExpense("alice");
        var bobExpense = await AddExpense("bob");
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            // Fail AFTER all SQL DELETEs succeeded, when saving the evidence record.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailDeletionRecord BEFORE INSERT ON AccountDeletionRecords BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
            await Assert.ThrowsAsync<DbUpdateException>(() => sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true));
            return true;
        });
        // A new scope reads persisted state, not entities cached by the failed operation.
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.AccountDeletionRecords.ToListAsync());
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.Equal(8, await db.Categories.CountAsync(c => c.UserId == "alice"));
            Assert.Equal(8, await db.Categories.CountAsync(c => c.UserId == "bob"));
            Assert.True(await db.Expenses.AnyAsync(e => e.Id == aliceExpense));
            Assert.True(await db.Expenses.AnyAsync(e => e.Id == bobExpense));
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync("alice"))!, _password));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailDeletionRecord;");
            return true;
        });
        Assert.Equal(ResultStatus.Success, (await As("alice", sp =>
            sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true))).Status);
        await As("bob", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("alice", Assert.Single(await db.AccountDeletionRecords.ToListAsync()).UserId);
            Assert.Equal("bob", Assert.Single(await db.Users.ToListAsync()).Id);
            Assert.Equal(bobExpense, Assert.Single(await db.Expenses.ToListAsync()).Id);
            return true;
        });
    }

    [Fact]
    public async Task RepeatedDeletion_DoesNotCreateDuplicateEvidence()
    {
        await AddExpense("alice");
        Assert.Equal(ResultStatus.Success, (await As("alice", sp =>
            sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true))).Status);
        // Simulate a stale authenticated request after the first deletion committed.
        Assert.Equal(ResultStatus.NotFound, (await As("alice", sp =>
            sp.GetRequiredService<AccountDeletionService>().DeleteAsync(_password, true))).Status);
        await As("bob", async sp =>
        {
            Assert.Single(await sp.GetRequiredService<ApplicationDbContext>().AccountDeletionRecords.ToListAsync());
            return true;
        });
    }

    [Fact]
    public async Task FormInput_PersistsExactCentsAndCannotBeReadByAnotherUser()
    {
        await Initialize("alice");
        var category = (await Categories("alice"))[0];
        var form = new SpendWise.Web.Components.Expenses.ExpenseFormModel(new FixedClock())
        {
            Amount = 24.50m, CategoryId = category.Id,
            Merchant = "  Grocery store  ", Notes = "  Weekly shopping  "
        };
        var result = await As("alice", sp => sp.GetRequiredService<ExpenseService>().CreateAsync(form.ToInput()));
        Assert.Equal(ResultStatus.Success, result.Status);
        // A fresh service/context reads the saved record, rather than form or tracked state.
        var saved = await As("alice", sp => sp.GetRequiredService<ExpenseService>().GetAsync(result.Value));
        Assert.Equal(2450, saved.Value!.AmountCents);
        Assert.Equal(Today, saved.Value.ExpenseDate);
        Assert.Equal(category.Id, saved.Value.CategoryId);
        Assert.Equal("Grocery store", saved.Value.Merchant);
        Assert.Equal("Weekly shopping", saved.Value.Notes);
        Assert.Equal(ResultStatus.NotFound, (await As("bob", sp =>
            sp.GetRequiredService<ExpenseService>().GetAsync(result.Value))).Status);
    }

    [Fact]
    public async Task Ledger_MonthBoundariesArchivedFiltersAndStableOrdering()
    {
        await Initialize("alice");
        await AddExpense("bob");
        var category = (await Categories("alice"))[0].Id;
        var other = (await Categories("alice"))[1].Id;
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            var october = await service.CreateAsync(new(1, new(2026, 10, 31), category));
            var first = await service.CreateAsync(new(29, new(2026, 11, 1), category));
            var second = await service.CreateAsync(new(2450, new(2026, 11, 1), category));
            var newest = await service.CreateAsync(new(99999999, Today, other));
            await sp.GetRequiredService<CategoryService>().SetArchivedAsync(category, true);
            var november = (await service.ListAsync(new(2026, 11))).Value!;
            Assert.Equal(3, november.TotalCount);
            Assert.Equal(new[] { newest.Value, second.Value, first.Value }, november.Items.Select(e => e.Id));
            Assert.Equal(october.Value, Assert.Single((await service.ListAsync(new(2026, 10))).Value!.Items).Id);
            var archived = (await service.ListAsync(new(2026, 11, category))).Value!;
            Assert.Equal(2, archived.TotalCount);
            Assert.Equal(new long[] { 2450, 29 }, archived.Items.Select(e => e.AmountCents));
            Assert.DoesNotContain(await sp.GetRequiredService<CategoryService>().ListAsync(), c => c.Id == category);
            Assert.Contains(await sp.GetRequiredService<CategoryService>().ListAsync(true), c => c.Id == category && c.IsArchived);
            return true;
        });
    }

    [Theory]
    [InlineData(1999, 12, 1)]
    [InlineData(2026, 0, 1)]
    [InlineData(2026, 13, 1)]
    [InlineData(2026, 12, 1)]
    [InlineData(2026, 11, 0)]
    public async Task Ledger_InvalidFiltersReturnValidation(int year, int month, int page)
    {
        var result = await As("alice", sp => sp.GetRequiredService<ExpenseService>().ListAsync(new(year, month, Page: page)));
        Assert.Equal(ResultStatus.ValidationFailure, result.Status);
    }

    [Fact]
    public async Task Edit_UpdatesEveryFieldPreservesOwnerAndCreationTimeAndMovesFilters()
    {
        var id = await AddExpense("alice");
        var other = (await Categories("alice"))[1].Id;
        await As("alice", async sp =>
        {
            var result = await sp.GetRequiredService<ExpenseService>().UpdateAsync(id,
                new(29, new(2026,10,31), other, "  New shop  ", "  Corrected  "));
            Assert.Equal(ResultStatus.Success, result.Status);
            return true;
        });
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var saved = await db.Expenses.SingleAsync(e => e.Id == id);
            Assert.Equal("alice", saved.UserId);
            Assert.Equal(new DateTime(2026,11,1,13,30,0), saved.CreatedAtUtc);
            Assert.Equal(29, saved.AmountCents);
            Assert.Equal("New shop", saved.Merchant);
            Assert.Equal("Corrected", saved.Notes);
            Assert.Equal(other, saved.CategoryId);
            var service = sp.GetRequiredService<ExpenseService>();
            Assert.Empty((await service.ListAsync(new(2026,11))).Value!.Items);
            Assert.Equal(id, Assert.Single((await service.ListAsync(new(2026,10,other))).Value!.Items).Id);
            return true;
        });
    }

    [Fact]
    public async Task InvalidEdit_PreservesStoredExpense_AndDeletedRecordIsUnavailable()
    {
        var id = await AddExpense("alice");
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            var original = (await service.GetAsync(id)).Value!;
            Assert.Equal(ResultStatus.ValidationFailure, (await service.UpdateAsync(id,
                new(0, Today, original.CategoryId, "Must not persist"))).Status);
            Assert.Equal(original, (await service.GetAsync(id)).Value);
            Assert.Equal(ResultStatus.Success, (await service.DeleteAsync(id)).Status);
            Assert.Equal(ResultStatus.NotFound, (await service.GetAsync(id)).Status);
            Assert.Equal(ResultStatus.NotFound, (await service.UpdateAsync(id, new(100, Today, original.CategoryId))).Status);
            Assert.Equal(ResultStatus.NotFound, (await service.DeleteAsync(id)).Status);
            return true;
        });
    }

    [Fact]
    public async Task DeletingLastRowOnSecondPage_LeavesTwentyRowsOnFirstPage()
    {
        await Initialize("alice");
        var category = (await Categories("alice"))[0].Id;
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            for (var i = 0; i < 21; i++) await service.CreateAsync(new(100, Today, category));
            var last = Assert.Single((await service.ListAsync(new(2026,11,Page:2))).Value!.Items);
            await service.DeleteAsync(last.Id);
            var second = (await service.ListAsync(new(2026,11,Page:2))).Value!;
            Assert.Equal(20, second.TotalCount);
            Assert.Empty(second.Items);
            Assert.Equal(20, (await service.ListAsync(new(2026,11))).Value!.Items.Count);
            return true;
        });
    }

    [Fact]
    public async Task Dashboard_ExactTotalsChangeAfterEditAndDelete_AndIncludeArchivedHistory()
    {
        await Initialize("alice");
        var categories = await Categories("alice");
        await As("alice", async sp =>
        {
            var expenses = sp.GetRequiredService<ExpenseService>();
            var dashboard = sp.GetRequiredService<DashboardService>();
            var edited = (await expenses.CreateAsync(new(2450, Today, categories[0].Id))).Value;
            var deleted = (await expenses.CreateAsync(new(1200, Today, categories[0].Id))).Value;
            await expenses.CreateAsync(new(8240, Today, categories[1].Id));
            await sp.GetRequiredService<CategoryService>().SetArchivedAsync(categories[1].Id, true);
            var initial = (await dashboard.GetMonthlySummaryAsync(2026, 11)).Value!;
            Assert.Equal(11890, initial.TotalCents);
            Assert.Equal(3, initial.ExpenseCount);
            Assert.Equal(new long[] { 8240, 3650 }, initial.Categories.Select(c => c.TotalCents));
            Assert.True(initial.Categories[0].IsArchived);
            Assert.Equal(2, initial.Categories[1].ExpenseCount);
            Assert.Equal(ResultStatus.Success, (await expenses.UpdateAsync(edited, new(2000, Today, categories[0].Id))).Status);
            Assert.Equal(11440, (await dashboard.GetMonthlySummaryAsync(2026, 11)).Value!.TotalCents);
            Assert.Equal(ResultStatus.Success, (await expenses.DeleteAsync(deleted)).Status);
            var final = (await dashboard.GetMonthlySummaryAsync(2026, 11)).Value!;
            Assert.Equal(10240, final.TotalCents);
            Assert.Equal(2, final.ExpenseCount);
            return true;
        });
    }

    [Fact]
    public async Task Dashboard_ScopesOwnerAndMonth_IncludesEveryPage_AndReturnsZeroForEmptyMonth()
    {
        await Initialize("alice");
        await AddExpense("bob", 99999);
        var category = (await Categories("alice"))[0].Id;
        await As("alice", async sp =>
        {
            var expenses = sp.GetRequiredService<ExpenseService>();
            // October has records on both boundaries, plus a 21st row beyond ledger page one.
            for (var i = 0; i < 21; i++)
                await expenses.CreateAsync(new(29, new DateOnly(2026, 10, i == 0 ? 1 : 31), category));
            await expenses.CreateAsync(new(700, new DateOnly(2026, 9, 30), category));
            await expenses.CreateAsync(new(900, new DateOnly(2026, 11, 1), category));
            var dashboard = sp.GetRequiredService<DashboardService>();
            var october = (await dashboard.GetMonthlySummaryAsync(2026, 10)).Value!;
            Assert.Equal(609, october.TotalCents);
            Assert.Equal(21, october.ExpenseCount);
            Assert.Equal(21, Assert.Single(october.Categories).ExpenseCount);
            Assert.Equal(900, (await dashboard.GetMonthlySummaryAsync(2026, 11)).Value!.TotalCents);
            var empty = (await dashboard.GetMonthlySummaryAsync(2026, 8)).Value!;
            Assert.Equal(0, empty.TotalCents);
            Assert.Equal(0, empty.ExpenseCount);
            Assert.Empty(empty.Categories);
            return true;
        });
    }

    [Theory]
    [InlineData(1999, 12)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(2026, 12)]
    [InlineData(2027, 1)]
    public async Task Dashboard_RejectsUnsupportedMonths(int year, int month)
    {
        var response = await As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(year, month));
        Assert.Equal(ResultStatus.ValidationFailure, response.Status);
    }

    [Fact]
    public async Task CategoryLifecycle_RenamePreservesHistory_ArchiveReservesName_RestoreAllowsNewExpenses()
    {
        await Initialize("alice");
        await As("alice", async sp =>
        {
            var categories = sp.GetRequiredService<CategoryService>();
            var expenses = sp.GetRequiredService<ExpenseService>();
            var created = await categories.CreateAsync("  Coffee  ");
            Assert.Equal(ResultStatus.Success, created.Status);
            var id = created.Value;
            Assert.Equal("Coffee", (await categories.ListAsync()).Single(c => c.Id == id).Name);
            var expense = await expenses.CreateAsync(new(450, Today, id));
            Assert.Equal(ResultStatus.Success, expense.Status);
            Assert.Equal(ResultStatus.Success, (await categories.RenameAsync(id, "  Cafe visits  ")).Status);
            var row = (await expenses.GetAsync(expense.Value)).Value!;
            Assert.Equal("Cafe visits", row.CategoryName);
            Assert.Equal(id, row.CategoryId);
            Assert.Equal(450, row.AmountCents);
            await categories.SetArchivedAsync(id, true);
            Assert.DoesNotContain(await categories.ListAsync(), c => c.Id == id);
            Assert.Equal(ResultStatus.ValidationFailure, (await categories.CreateAsync("CAFE VISITS")).Status);
            var other = (await categories.ListAsync())[0];
            Assert.Equal(ResultStatus.ValidationFailure, (await categories.RenameAsync(other.Id, "cafe visits")).Status);
            Assert.Equal(other.Name, (await categories.ListAsync()).Single(c => c.Id == other.Id).Name);
            // Archived categories can be renamed without changing the expense's identity or value.
            Assert.Equal(ResultStatus.Success, (await categories.RenameAsync(id, "Coffee breaks")).Status);
            var summary = (await sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026, 11)).Value!;
            Assert.Equal(450, summary.TotalCents);
            Assert.Equal("Coffee breaks", Assert.Single(summary.Categories).CategoryName);
            Assert.True(summary.Categories[0].IsArchived);
            Assert.Equal(ResultStatus.ValidationFailure, (await expenses.CreateAsync(new(100, Today, id))).Status);
            Assert.Equal(ResultStatus.Success, (await categories.SetArchivedAsync(id, false)).Status);
            Assert.Contains(await categories.ListAsync(), c => c.Id == id && !c.IsArchived);
            Assert.Equal(ResultStatus.Success, (await expenses.CreateAsync(new(100, Today, id))).Status);
            return true;
        });
        // The same name belongs to a different user's namespace, so it remains available.
        var bob = await As("bob", sp => sp.GetRequiredService<CategoryService>().CreateAsync("Coffee breaks"));
        Assert.Equal(ResultStatus.Success, bob.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public async Task CategoryInvalidNames_DoNotCreateOrRename(string name)
    {
        await Initialize("alice");
        var before = await Categories("alice");
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<CategoryService>();
            Assert.Equal(ResultStatus.ValidationFailure, (await service.CreateAsync(name)).Status);
            Assert.Equal(ResultStatus.ValidationFailure, (await service.RenameAsync(before[0].Id, name)).Status);
            return true;
        });
        Assert.Equal(before, await Categories("alice"));
    }

    private sealed class TestAuthentication : AuthenticationStateProvider
    {
        public string? UserId { get; set; }
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(UserId is null ? new ClaimsIdentity() :
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "test"))));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,11,1,13,30,0,TimeSpan.Zero);
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpendWise.Web.Data;

namespace SpendWise.Tests.Integration;

public class IdentityDatabaseTests
{
    [Fact]
    public async Task Migrations_UpgradeIdentityDatabase_AndPreserveExistingUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>(options =>
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Reproduce an existing installation before the expense tables were introduced.
            await db.GetService<IMigrator>().MigrateAsync("00000000000000_CreateIdentitySchema");
            db.Users.Add(new ApplicationUser
            {
                Id = "setup-test-user", UserName = "setup@example.invalid",
                NormalizedUserName = "SETUP@EXAMPLE.INVALID",
                Email = "setup@example.invalid", NormalizedEmail = "SETUP@EXAMPLE.INVALID"
            });
            await db.SaveChangesAsync();
        }
        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Upgrade after the account exists; new tables must not alter its stored details.
        await verification.Database.MigrateAsync();
        Assert.Equal("setup@example.invalid", (await verification.Users.SingleAsync()).Email);
        Assert.Empty(await verification.Categories.ToListAsync());
        Assert.Empty(await verification.Expenses.ToListAsync());
        Assert.Empty(await verification.Database.GetPendingMigrationsAsync());
    }
}

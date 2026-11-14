using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Web.Contracts;
using SpendWise.Web.Data;
using SpendWise.Web.Models;
using SpendWise.Web.Services;

namespace SpendWise.Tests.Integration;

public sealed partial class UserServicesTests
{
    [Fact]
    public async Task Acceptance_ExactOctoberFixtureReconcilesAfterEditAndDelete()
    {
        await Initialize("alice");
        await Initialize("bob");
        var categories = await Categories("alice");
        var groceries = categories.Single(c => c.Name == "Groceries").Id;
        var dining = categories.Single(c => c.Name == "Dining").Id;
        var transport = categories.Single(c => c.Name == "Transport").Id;
        int diningId = 0, transportId = 0;
        await As("alice", async sp =>
        {
            var service = sp.GetRequiredService<ExpenseService>();
            Assert.Equal(ResultStatus.Success, (await service.CreateAsync(new(8240, new(2026, 10, 3), groceries))).Status);
            transportId = (await service.CreateAsync(new(1200, new(2026, 10, 4), transport))).Value;
            diningId = (await service.CreateAsync(new(2450, new(2026, 10, 5), dining))).Value;
            await service.CreateAsync(new(900, new(2026, 9, 30), groceries));
            return true;
        });
        var bobCategory = (await Categories("bob"))[0].Id;
        await As("bob", sp => sp.GetRequiredService<ExpenseService>().CreateAsync(new(10000, new(2026,10,3), bobCategory)));
        // Separate service scopes/connections prove results are persisted, not just tracked in memory.
        var initial = (await As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026,10))).Value!;
        Assert.Equal(11890, initial.TotalCents);
        Assert.Equal(3, initial.ExpenseCount);
        Assert.Equal(new long[] { 8240, 2450, 1200 }, initial.Categories.Select(c => c.TotalCents));
        var filtered = (await As("alice", sp => sp.GetRequiredService<ExpenseService>().ListAsync(new(2026,10,dining)))).Value!;
        Assert.Equal(2450, Assert.Single(filtered.Items).AmountCents);
        Assert.Equal(11890, (await As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026,10))).Value!.TotalCents);
        Assert.Equal(ResultStatus.Success, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().UpdateAsync(diningId,new(2000,new(2026,10,5),dining)))).Status);
        Assert.Equal(11440, (await As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026,10))).Value!.TotalCents);
        Assert.Equal(ResultStatus.Success, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().DeleteAsync(transportId))).Status);
        var final = (await As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026,10))).Value!;
        Assert.Equal(10240, final.TotalCents);
        Assert.Equal(2, final.ExpenseCount);
        Assert.Equal(final.TotalCents, final.Categories.Sum(c => c.TotalCents));
        Assert.Equal(ResultStatus.NotFound, (await As("alice", sp => sp.GetRequiredService<ExpenseService>().GetAsync(transportId))).Status);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Acceptance_FiveThousandExpenses_ReportWarmServiceTimingsAndVerifyResults()
    {
        await Initialize("alice");
        await Initialize("bob");
        var categories = await Categories("alice");
        // Deterministic source data provides an independent in-memory oracle for SQL results.
        var source = Enumerable.Range(0, 5000).Select(i => new Expense
        {
            UserId = "alice", CategoryId = categories[i % categories.Count].Id,
            AmountCents = 1 + (i * 37L % 100000),
            ExpenseDate = new DateOnly(2026, i < 2500 ? 10 : i < 4000 ? 9 : 8, 1 + i % 28),
            Merchant = $"Invented merchant {i}", CreatedAtUtc = new DateTime(2026,11,1), UpdatedAtUtc = new DateTime(2026,11,1)
        }).ToArray();
        await As("alice", async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.Expenses.AddRange(source);
            await db.SaveChangesAsync();
            return true;
        });
        // A second owner's same-month expense must never enter Alice's counts or totals.
        var bobCategory = (await Categories("bob"))[0].Id;
        await As("bob", sp => sp.GetRequiredService<ExpenseService>().CreateAsync(new(99999999,new(2026,10,1),bobCategory)));
        var october = source.Where(e => e.ExpenseDate.Month == 10).ToArray();
        var ordered = october.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id).ToArray();
        output.WriteLine($"ENV: {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.FrameworkDescription}; CPU logical count={Environment.ProcessorCount}");
        output.WriteLine("DATASET: Alice=5000; October=2500, September=1500, August=1000; categories=8; Bob=1 control; file-backed SQLite, Pooling=False; Release recommended.");
        output.WriteLine($"ORACLE: October cents={october.Sum(e => e.AmountCents)}; count={october.Length}");

        foreach (var page in new[] { 1, 63, 125 })
        {
            await Measure($"ledger-page-{page}", () => As("alice", sp => sp.GetRequiredService<ExpenseService>().ListAsync(new(2026,10,Page:page))), response =>
            {
                Assert.Equal(ResultStatus.Success, response.Status);
                Assert.Equal(2500, response.Value!.TotalCount);
                Assert.Equal(ordered.Skip((page-1)*20).Take(20).Select(e => e.Id), response.Value.Items.Select(e => e.Id));
            });
        }
        await Measure("ledger-category", () => As("alice", sp => sp.GetRequiredService<ExpenseService>().ListAsync(new(2026,10,categories[0].Id))), response =>
        {
            var expected = ordered.Where(e => e.CategoryId == categories[0].Id).ToArray();
            Assert.Equal(ResultStatus.Success, response.Status);
            Assert.Equal(expected.Length, response.Value!.TotalCount);
            Assert.Equal(expected.Take(20).Select(e => e.Id), response.Value.Items.Select(e => e.Id));
        });
        await Measure("dashboard", () => As("alice", sp => sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(2026,10)), response =>
        {
            Assert.Equal(ResultStatus.Success, response.Status);
            Assert.Equal(october.Sum(e => e.AmountCents), response.Value!.TotalCents);
            Assert.Equal(2500, response.Value.ExpenseCount);
            foreach (var category in response.Value.Categories)
            {
                var expected = october.Where(e => e.CategoryId == category.CategoryId).ToArray();
                Assert.Equal(expected.Sum(e => e.AmountCents), category.TotalCents);
                Assert.Equal(expected.Length, category.ExpenseCount);
            }
        });
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 12, 31)]
    [InlineData(2026, 1, 31)]
    public async Task Acceptance_LeapAndYearBoundariesIncludeOnlySelectedMonth(int year, int month, int lastDay)
    {
        await Initialize("alice");
        var category = (await Categories("alice"))[0].Id;
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, lastDay);
        await As("alice", async sp =>
        {
            var expenses = sp.GetRequiredService<ExpenseService>();
            foreach (var date in new[] { start.AddDays(-1), start, end, end.AddDays(1) })
                Assert.Equal(ResultStatus.Success, (await expenses.CreateAsync(new(29, date, category))).Status);
            var rows = (await expenses.ListAsync(new(year,month))).Value!;
            Assert.Equal(new[] { end, start }, rows.Items.Select(e => e.ExpenseDate));
            var summary = (await sp.GetRequiredService<DashboardService>().GetMonthlySummaryAsync(year,month)).Value!;
            Assert.Equal(58, summary.TotalCents);
            Assert.Equal(2, summary.ExpenseCount);
            return true;
        });
    }

    private async Task Measure<T>(string name, Func<Task<T>> action, Action<T> verify)
    {
        verify(await action()); // Warm EF query compilation and SQLite/page caches before sampling.
        var timings = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            var result = await action();
            watch.Stop();
            timings.Add(watch.Elapsed.TotalMilliseconds);
            verify(result); // Assertions are outside the timed interval.
        }
        var sorted = timings.Order().ToArray();
        var median = (sorted[9] + sorted[10]) / 2;
        var p95 = sorted[18]; // Nearest rank: ceil(0.95 * 20) = sample 19.
        output.WriteLine($"PERF {name}: n=20 median={median:F3}ms p95={p95:F3}ms max={sorted[^1]:F3}ms p95Target500ms={(p95 <= 500 ? "PASS" : "MISS")}");
        output.WriteLine($"SAMPLES {name}: {string.Join(",", timings.Select(t => t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))}");
        // Timing is evidence, not a flaky CI assertion. A target miss must be reviewed in acceptance.
    }
}

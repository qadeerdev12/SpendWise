using System.ComponentModel.DataAnnotations;
using System.Globalization;
using SpendWise.Web.Components.Expenses;

namespace SpendWise.Tests;

public sealed class ExpenseFormModelTests
{
    [Theory]
    [InlineData("0.01", 1)]
    [InlineData("24.50", 2450)]
    [InlineData("0.29", 29)]
    [InlineData("999999.99", 99999999)]
    public void ValidAmounts_ConvertToExactCents(string dollars, long cents)
    {
        var model = ValidModel();
        model.Amount = decimal.Parse(dollars, CultureInfo.InvariantCulture);
        Assert.Equal(cents, model.ToInput().AmountCents);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000")]
    [InlineData("24.505")]
    [InlineData("0.001")]
    [InlineData("79228162514264337593543950335")]
    public void InvalidAmounts_CannotBeRoundedTruncatedOrOverflowed(string dollars)
    {
        var model = ValidModel();
        model.Amount = decimal.Parse(dollars, CultureInfo.InvariantCulture);
        Assert.Throws<ValidationException>(() => model.ToInput());
    }

    [Fact]
    public void RequiredValues_CannotBeConverted()
    {
        var model = ValidModel();
        model.Amount = null;
        Assert.Throws<ValidationException>(() => model.ToInput());
        model = ValidModel();
        model.ExpenseDate = null;
        Assert.Throws<ValidationException>(() => model.ToInput());
        model = ValidModel();
        model.CategoryId = 0;
        Assert.Throws<ValidationException>(() => model.ToInput());
    }

    [Fact]
    public void DateDefaultsAndValidation_UseSydneyNotUtc()
    {
        var model = ValidModel();
        // The clock is still 1 November in UTC, but already 2 November in Sydney.
        Assert.Equal(new DateOnly(2026, 11, 2), model.ExpenseDate);
        model.ExpenseDate = new(2026, 11, 3);
        Assert.Throws<ValidationException>(() => model.ToInput());
        model.ExpenseDate = new(1999, 12, 31);
        Assert.Throws<ValidationException>(() => model.ToInput());
        model.ExpenseDate = new(2000, 1, 1);
        Assert.Equal(model.ExpenseDate, model.ToInput().ExpenseDate);
    }

    [Fact]
    public void DateLimit_IsReevaluatedAfterMidnight()
    {
        var clock = new TestClock();
        var model = new ExpenseFormModel(clock) { Amount = 1m, CategoryId = 1, ExpenseDate = new(2026, 11, 3) };
        Assert.Throws<ValidationException>(() => model.ToInput());
        clock.UtcNow = clock.UtcNow.AddDays(1);
        Assert.Equal(new DateOnly(2026, 11, 3), model.ToInput().ExpenseDate);
    }

    [Fact]
    public void TextLimits_UseTrimmedLengthsLikeTheService()
    {
        var model = ValidModel();
        model.Merchant = new string('m', 101);
        Assert.Throws<ValidationException>(() => model.ToInput());
        model.Merchant = " " + new string('m', 100) + " ";
        model.Notes = new string('n', 501);
        Assert.Throws<ValidationException>(() => model.ToInput());
        model.Notes = " " + new string('n', 500) + " ";
        Assert.Equal(model.Merchant, model.ToInput().Merchant);
    }

    private static ExpenseFormModel ValidModel() => new(new TestClock()) { Amount = 24.50m, CategoryId = 1 };
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 1, 13, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

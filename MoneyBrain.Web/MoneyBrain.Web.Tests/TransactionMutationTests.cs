using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Exercises transaction atomicity and normalized category/split mutations with real SQLite constraints.</summary>
[TestClass]
public sealed class TransactionMutationTests
{
    /// <summary>A database ledger failure rolls back the newly inserted transaction and its splits.</summary>
    [TestMethod]
    public async Task Ledger_insert_failure_rolls_back_transaction_and_splits()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        await using (var context = database.CreateDbContext())
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_ledger_insert BEFORE INSERT ON LedgerEntries
                BEGIN SELECT RAISE(ABORT, 'Test ledger constraint'); END;
                """);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
                database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
                TransactionStatus.Posted, false, null, null,
                [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m }]));

        await using var verification = database.CreateDbContext();
        Assert.AreEqual(0, await verification.Transactions.CountAsync());
        Assert.AreEqual(0, await verification.TransactionSplits.CountAsync());
        Assert.AreEqual(0, await verification.LedgerEntries.CountAsync());
    }

    /// <summary>A failed regeneration restores the original amount, memo and original ledger entries.</summary>
    [TestMethod]
    public async Task Ledger_regeneration_failure_rolls_back_transaction_update_and_deleted_ledger()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync();
        List<int> originalLedgerIds;
        await using (var context = database.CreateDbContext())
        {
            originalLedgerIds = await context.LedgerEntries.OrderBy(entry => entry.Id).Select(entry => entry.Id).ToListAsync();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_ledger_insert BEFORE INSERT ON LedgerEntries
                BEGIN SELECT RAISE(ABORT, 'Test ledger constraint'); END;
                """);
        }

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() =>
            database.Transactions.UpdateTransactionAsync(transaction.Id, FinanceDatabase.Owner,
                transaction.Date, -200m, null, database.Category.Id, "Changed",
                TransactionStatus.Posted, false, null, null));

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.SingleAsync();
        Assert.AreEqual(-100m, stored.Amount);
        Assert.AreEqual("Original memo", stored.Memo);
        CollectionAssert.AreEqual(originalLedgerIds,
            await verification.LedgerEntries.OrderBy(entry => entry.Id).Select(entry => entry.Id).ToListAsync());
    }

    /// <summary>Expense and income lines normalize independently before the signed total is validated.</summary>
    [TestMethod]
    public async Task Mixed_sign_split_categories_normalize_to_exact_parent_total()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var income = await AddIncomeCategoryAsync(database);

        var transaction = await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -90m, null, null,
            TransactionStatus.Posted, false, null, null,
            [
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = 100m },
                new TransactionSplitDto { CategoryId = income.Id, Amount = 10m }
            ]);

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.Include(item => item.Splits).SingleAsync();
        Assert.AreEqual(transaction.Id, stored.Id);
        Assert.AreEqual(-90m, stored.Splits.Sum(split => split.Amount));
        Assert.AreEqual(-100m, stored.Splits.Single(split => split.CategoryId == database.Category.Id).Amount);
        Assert.AreEqual(10m, stored.Splits.Single(split => split.CategoryId == income.Id).Amount);
        var ledger = await verification.LedgerEntries.ToListAsync();
        Assert.HasCount(2, ledger.Where(entry => entry.CategoryId.HasValue).ToList());
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    /// <summary>An apparently matching raw total is rejected when category normalization changes the sum.</summary>
    [TestMethod]
    public async Task Category_normalized_split_mismatch_is_rejected_without_writes()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var income = await AddIncomeCategoryAsync(database);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
                database.Checking.Id, new DateTime(2026, 1, 15), -110m, null, null,
                TransactionStatus.Posted, false, null, null,
                [
                    new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m },
                    new TransactionSplitDto { CategoryId = income.Id, Amount = -10m }
                ]));

        await using var verification = database.CreateDbContext();
        Assert.AreEqual(0, await verification.Transactions.CountAsync());
        Assert.AreEqual(0, await verification.TransactionSplits.CountAsync());
    }

    /// <summary>A category-type bulk change updates both the signed amount and category ledger.</summary>
    [TestMethod]
    public async Task Bulk_category_change_normalizes_amount_and_regenerates_ledger()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var income = await AddIncomeCategoryAsync(database);

        await database.Transactions.BulkUpdateTransactionsAsync(FinanceDatabase.Owner,
            new BulkEditTransactionRequest { TransactionIds = [transaction.Id], CategoryId = income.Id });

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.SingleAsync();
        Assert.AreEqual(income.Id, stored.CategoryId);
        Assert.AreEqual(100m, stored.Amount);
        var ledger = await verification.LedgerEntries.ToListAsync();
        Assert.IsFalse(ledger.Any(entry => entry.CategoryId == database.Category.Id));
        Assert.IsTrue(ledger.Any(entry => entry.CategoryId == income.Id));
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    /// <summary>Rounding must not hide a missing cent or any sub-cent split amount.</summary>
    [TestMethod]
    [DataRow("50.00", "49.995")]
    [DataRow("50.00", "49.99")]
    [DataRow("50.005", "49.995")]
    public async Task Near_miss_or_subcent_splits_reject_creation_without_persisting_rows(string first, string second)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var splits = new List<TransactionSplitDto>
        {
            new() { CategoryId = database.Category.Id, Amount = decimal.Parse(first, System.Globalization.CultureInfo.InvariantCulture) },
            new() { CategoryId = database.Category.Id, Amount = decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture) }
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
                database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
                TransactionStatus.Posted, false, null, null, splits));

        await using var verification = database.CreateDbContext();
        Assert.AreEqual(0, await verification.Transactions.CountAsync());
        Assert.AreEqual(0, await verification.TransactionSplits.CountAsync());
        Assert.AreEqual(0, await verification.LedgerEntries.CountAsync());
    }

    /// <summary>Invalid replacement splits cannot change the parent, original splits or ledger.</summary>
    [TestMethod]
    [DataRow("50.00", "49.995")]
    [DataRow("50.00", "49.99")]
    [DataRow("50.005", "49.995")]
    public async Task Near_miss_or_subcent_splits_reject_update_without_changing_original(string first, string second)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, "Original",
            TransactionStatus.Posted, false, null, null,
            [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m }]);
        var splits = new List<TransactionSplitDto>
        {
            new() { CategoryId = database.Category.Id, Amount = decimal.Parse(first, System.Globalization.CultureInfo.InvariantCulture) },
            new() { CategoryId = database.Category.Id, Amount = decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture) }
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.UpdateTransactionWithSplitsAsync(transaction.Id, FinanceDatabase.Owner,
                transaction.Date, -100m, null, "Changed", TransactionStatus.Posted, false, null, null, splits));

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.Include(item => item.Splits).SingleAsync();
        Assert.AreEqual("Original", stored.Memo);
        Assert.AreEqual(-100m, stored.Amount);
        Assert.HasCount(1, stored.Splits);
        Assert.AreEqual(-100m, stored.Splits.Single().Amount);
        var ledger = await verification.LedgerEntries.ToListAsync();
        Assert.HasCount(2, ledger);
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    /// <summary>Exact-cent cashback is a valid mixed-sign split, not a rounding discrepancy.</summary>
    [TestMethod]
    public async Task Cashback_split_preserves_exact_expense_and_income_lines()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var income = await AddIncomeCategoryAsync(database);

        await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, "Cashback",
            TransactionStatus.Posted, false, null, null,
            [
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -110m },
                new TransactionSplitDto { CategoryId = income.Id, Amount = 10m }
            ]);

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.Include(item => item.Splits).SingleAsync();
        Assert.AreEqual(-110m, stored.Splits.Single(split => split.CategoryId == database.Category.Id).Amount);
        Assert.AreEqual(10m, stored.Splits.Single(split => split.CategoryId == income.Id).Amount);
        Assert.AreEqual(stored.Amount, stored.Splits.Sum(split => split.Amount));
        var ledger = await verification.LedgerEntries.ToListAsync();
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    private static async Task<Category> AddIncomeCategoryAsync(FinanceDatabase database)
    {
        var income = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Income",
            CategoryGroup = new CategoryGroup
            {
                UserId = FinanceDatabase.Owner, Name = "Income group", Type = CategoryType.Income
            }
        };
        await using var context = database.CreateDbContext();
        context.Categories.Add(income);
        await context.SaveChangesAsync();
        return income;
    }
}

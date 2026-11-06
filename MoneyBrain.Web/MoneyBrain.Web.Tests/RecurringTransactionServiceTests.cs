using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Application.Transactions.RecurringTransactions;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Verifies retry-safe recurrence and split preservation through the real application service.</summary>
[TestClass]
public sealed class RecurringTransactionServiceTests
{
    /// <summary>Both a retry and resetting the stored cursor cannot create duplicate occurrences.</summary>
    [TestMethod]
    public async Task Retry_and_reset_cursor_do_not_duplicate_generated_occurrences()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 1, 1);
        var template = await database.Transactions.CreateTransactionAsync(FinanceDatabase.Owner,
            database.Checking.Id, start, -100m, null, database.Category.Id, "Recurring",
            TransactionStatus.Posted, false, null, null, true, RecurrenceFrequency.Monthly, start);
        var service = Service(database);

        Assert.AreEqual(3, await service.GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start.AddMonths(2)));
        Assert.AreEqual(0, await service.GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start.AddMonths(2)));
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Transactions.SingleAsync(item => item.Id == template.Id);
            stored.NextRecurrenceDate = start;
            await context.SaveChangesAsync();
        }
        Assert.AreEqual(0, await service.GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start.AddMonths(2)));

        await using var verification = database.CreateDbContext();
        var generated = await verification.Transactions.Where(item => item.RecurringTemplateId == template.Id).ToListAsync();
        Assert.HasCount(3, generated);
        Assert.HasCount(3, generated.Select(item => item.Date).Distinct().ToList());
    }

    /// <summary>Locked templates produce due occurrences without changing the locked row or duplicating on retry.</summary>
    [TestMethod]
    public async Task Reconciled_template_generates_once_without_mutating_its_cursor_or_timestamp()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 1, 1);
        var template = await database.Transactions.CreateTransactionAsync(FinanceDatabase.Owner,
            database.Checking.Id, start, -100m, null, database.Category.Id, "Locked recurring",
            TransactionStatus.Posted, false, null, null, true, RecurrenceFrequency.Monthly, start);
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0);
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Transactions.SingleAsync(item => item.Id == template.Id);
            stored.IsReconciled = true;
            stored.UpdatedAt = timestamp;
            await context.SaveChangesAsync();
        }
        var service = Service(database);

        Assert.AreEqual(2, await service.GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start.AddMonths(1)));
        Assert.AreEqual(0, await service.GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start.AddMonths(1)));

        await using var verification = database.CreateDbContext();
        var persisted = await verification.Transactions.SingleAsync(item => item.Id == template.Id);
        Assert.AreEqual(start, persisted.NextRecurrenceDate);
        Assert.AreEqual(timestamp, persisted.UpdatedAt);
        Assert.IsTrue(persisted.IsReconciled);
        Assert.AreEqual(2, await verification.Transactions.CountAsync(item => item.RecurringTemplateId == template.Id));
    }

    /// <summary>Generated split occurrences retain their category lines and balanced category ledger.</summary>
    [TestMethod]
    public async Task Split_template_copies_lines_and_generates_balanced_ledger_for_occurrence()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 1, 1);
        var template = await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, start, -100m, null, "Split recurring", TransactionStatus.Posted, false, null, null,
            [
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -40m, Memo = "First line" },
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -60m, Memo = "Second line" }
            ]);
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Transactions.SingleAsync(item => item.Id == template.Id);
            stored.IsRecurring = true;
            stored.RecurrenceFrequency = RecurrenceFrequency.Monthly;
            stored.RecurrenceStartDate = start;
            stored.NextRecurrenceDate = start;
            await context.SaveChangesAsync();
        }

        Assert.AreEqual(1, await Service(database).GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start));

        await using var verification = database.CreateDbContext();
        var occurrence = await verification.Transactions.Include(item => item.Splits)
            .SingleAsync(item => item.RecurringTemplateId == template.Id);
        Assert.AreEqual(-100m, occurrence.Amount);
        Assert.HasCount(2, occurrence.Splits);
        Assert.AreEqual(occurrence.Amount, occurrence.Splits.Sum(split => split.Amount));
        CollectionAssert.AreEquivalent(new[] { "First line", "Second line" },
            occurrence.Splits.Select(split => split.Memo).ToArray());
        var ledger = await verification.LedgerEntries.Where(entry => entry.TransactionId == occurrence.Id).ToListAsync();
        Assert.AreEqual(2, ledger.Count(entry => entry.CategoryId == database.Category.Id));
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    private static RecurringTransactionService Service(FinanceDatabase database) => new(database,
        new LedgerService(database), database.Cache, NullLogger<RecurringTransactionService>.Instance);

    /// <summary>Committed occurrence generation invalidates touched months and preserves unrelated month/user caches.</summary>
    [TestMethod]
    public async Task Generation_invalidates_only_affected_months_and_owner_networth_cache()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 1, 1);
        await database.Transactions.CreateTransactionAsync(FinanceDatabase.Owner,
            database.Checking.Id, start, -100m, null, database.Category.Id, "Recurring",
            TransactionStatus.Posted, false, null, null, true, RecurrenceFrequency.Monthly, start);
        var january = CacheKeyHelper.ForMonthCashflow(FinanceDatabase.Owner, 2026, 1);
        var januaryBudget = CacheKeyHelper.ForBudgetComparison(FinanceDatabase.Owner, 2026, 1);
        var february = CacheKeyHelper.ForMonthCashflow(FinanceDatabase.Owner, 2026, 2);
        var networth = CacheKeyHelper.ForNetWorthSnapshot(FinanceDatabase.Owner, start);
        var foreign = CacheKeyHelper.ForNetWorthSnapshot(FinanceDatabase.Other, start);
        foreach (var key in new[] { january, januaryBudget, february, networth, foreign })
            await database.Cache.SetAsync(key, "cached");

        Assert.AreEqual(1, await Service(database).GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start));

        Assert.IsNull(await database.Cache.GetAsync<string>(january));
        Assert.IsNull(await database.Cache.GetAsync<string>(januaryBudget));
        Assert.IsNull(await database.Cache.GetAsync<string>(networth));
        Assert.AreEqual("cached", await database.Cache.GetAsync<string>(february));
        Assert.AreEqual("cached", await database.Cache.GetAsync<string>(foreign));
        await database.Cache.SetAsync(january, "fresh");
        Assert.AreEqual(0, await Service(database).GenerateDueRecurringTransactionsAsync(FinanceDatabase.Owner, start));
        Assert.AreEqual("fresh", await database.Cache.GetAsync<string>(january));
    }
}

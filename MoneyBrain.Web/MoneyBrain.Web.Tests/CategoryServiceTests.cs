using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Protects reconciled category history across ordinary, split and ledger references.</summary>
[TestClass]
public sealed class CategoryServiceTests
{
    /// <summary>A merge touching any reconciled source reference is rejected without partial archival.</summary>
    [TestMethod]
    [DataRow("transaction")]
    [DataRow("split")]
    [DataRow("ledger")]
    public async Task Reconciled_category_reference_rejects_merge_without_changing_history(string reference)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var target = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Target category",
            CategoryGroupId = database.Category.CategoryGroupId
        };
        await using (var context = database.CreateDbContext())
        {
            context.Categories.Add(target);
            await context.SaveChangesAsync();
        }
        var transaction = await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
            TransactionStatus.Posted, false, null, null,
            [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m }]);
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Transactions.Include(item => item.Splits).SingleAsync();
            stored.IsReconciled = true;
            stored.CategoryId = reference == "transaction" ? database.Category.Id : target.Id;
            stored.Splits.Single().CategoryId = reference == "split" ? database.Category.Id : target.Id;
            var entries = await context.LedgerEntries.Where(entry => entry.CategoryId.HasValue).ToListAsync();
            foreach (var entry in entries)
                entry.CategoryId = reference == "ledger" ? database.Category.Id : target.Id;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new CategoryService(database, database.Cache).MergeCategoriesAsync(
                database.Category.Id, target.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        Assert.IsTrue((await verification.Categories.SingleAsync(category => category.Id == database.Category.Id)).IsActive);
        var persisted = await verification.Transactions.Include(item => item.Splits).SingleAsync();
        Assert.AreEqual(transaction.Id, persisted.Id);
        Assert.AreEqual(reference == "transaction" ? database.Category.Id : target.Id, persisted.CategoryId);
        Assert.AreEqual(reference == "split" ? database.Category.Id : target.Id, persisted.Splits.Single().CategoryId);
        Assert.IsTrue(persisted.IsReconciled);
        Assert.AreEqual(1, await verification.LedgerEntries.CountAsync(entry =>
            entry.CategoryId == (reference == "ledger" ? database.Category.Id : target.Id)));
    }

    /// <summary>Unlocked history moves ordinary/split/ledger references and archives the source.</summary>
    [TestMethod]
    public async Task Unlocked_category_merge_moves_transaction_splits_and_ledger_together()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var ordinary = await database.CreateTransactionAsync();
        await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
            TransactionStatus.Posted, false, null, null,
            [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m }]);
        var target = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Target category",
            CategoryGroupId = database.Category.CategoryGroupId
        };
        await using (var context = database.CreateDbContext())
        {
            context.Categories.Add(target);
            await context.SaveChangesAsync();
        }

        Assert.IsTrue(await new CategoryService(database, database.Cache).MergeCategoriesAsync(
            database.Category.Id, target.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        Assert.IsFalse((await verification.Categories.SingleAsync(category => category.Id == database.Category.Id)).IsActive);
        Assert.AreEqual(target.Id, (await verification.Transactions.SingleAsync(item => item.Id == ordinary.Id)).CategoryId);
        Assert.IsTrue((await verification.TransactionSplits.ToListAsync()).All(split => split.CategoryId == target.Id));
        Assert.AreEqual(0, await verification.LedgerEntries.CountAsync(entry => entry.CategoryId == database.Category.Id));
        Assert.AreEqual(2, await verification.LedgerEntries.CountAsync(entry => entry.CategoryId == target.Id));
    }

    /// <summary>Merging income and expense categories cannot reinterpret existing signs or modify references.</summary>
    [TestMethod]
    public async Task Cross_type_merge_is_rejected_without_changing_transaction_ledger_or_source()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync();
        var target = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Income target",
            CategoryGroup = new CategoryGroup
            {
                UserId = FinanceDatabase.Owner, Name = "Income group", Type = CategoryType.Income
            }
        };
        await using (var context = database.CreateDbContext())
        {
            context.Categories.Add(target);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new CategoryService(database, database.Cache).MergeCategoriesAsync(
                database.Category.Id, target.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        var stored = await verification.Transactions.SingleAsync(item => item.Id == transaction.Id);
        Assert.AreEqual(database.Category.Id, stored.CategoryId);
        Assert.AreEqual(-100m, stored.Amount);
        Assert.IsTrue((await verification.Categories.SingleAsync(category => category.Id == database.Category.Id)).IsActive);
        Assert.AreEqual(0, await verification.LedgerEntries.CountAsync(entry => entry.CategoryId == target.Id));
        Assert.AreEqual(1, await verification.LedgerEntries.CountAsync(entry => entry.CategoryId == database.Category.Id));
    }

    /// <summary>Merge invalidation includes months represented only by split references, not just ordinary transactions.</summary>
    [TestMethod]
    public async Task Merge_invalidates_direct_and_split_only_months_and_owner_networth_cache()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        await database.CreateTransactionAsync();
        await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 2, 15), -100m, null, null,
            TransactionStatus.Posted, false, null, null,
            [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -100m }]);
        var target = new Category
        {
            UserId = FinanceDatabase.Owner, Name = "Target category",
            CategoryGroupId = database.Category.CategoryGroupId
        };
        await using (var context = database.CreateDbContext())
        {
            context.Categories.Add(target);
            await context.SaveChangesAsync();
        }
        var evicted = new[]
        {
            CacheKeyHelper.ForUserCategories(FinanceDatabase.Owner),
            CacheKeyHelper.ForMonthCashflow(FinanceDatabase.Owner, 2026, 1),
            CacheKeyHelper.ForMonthCashflow(FinanceDatabase.Owner, 2026, 2),
            CacheKeyHelper.ForBudgetComparison(FinanceDatabase.Owner, 2026, 1),
            CacheKeyHelper.ForBudgetComparison(FinanceDatabase.Owner, 2026, 2),
            CacheKeyHelper.ForBudgetComparison(FinanceDatabase.Owner, 2026, 8),
            CacheKeyHelper.ForNetWorthSnapshot(FinanceDatabase.Owner, new DateTime(2026, 1, 1))
        };
        var preserved = CacheKeyHelper.ForMonthCashflow(FinanceDatabase.Owner, 2026, 3);
        var foreignBudget = CacheKeyHelper.ForBudgetComparison(FinanceDatabase.Other, 2026, 8);
        foreach (var key in evicted.Append(preserved).Append(foreignBudget))
            await database.Cache.SetAsync(key, "cached");

        await new CategoryService(database, database.Cache).MergeCategoriesAsync(
            database.Category.Id, target.Id, FinanceDatabase.Owner);

        foreach (var key in evicted)
            Assert.IsNull(await database.Cache.GetAsync<string>(key), key);
        Assert.AreEqual("cached", await database.Cache.GetAsync<string>(preserved));
        Assert.AreEqual("cached", await database.Cache.GetAsync<string>(foreignBudget));
    }
}

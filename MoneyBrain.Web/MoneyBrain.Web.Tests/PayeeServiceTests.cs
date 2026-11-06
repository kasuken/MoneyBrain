using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Verifies payee merge ownership, input validation and reconciled-row immutability.</summary>
[TestClass]
public sealed class PayeeServiceTests
{
    /// <summary>A merge moves editable transactions while retaining the original payee on locked entries.</summary>
    [TestMethod]
    public async Task Merge_updates_editable_transactions_but_preserves_reconciled_payee_and_timestamp()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new PayeeService(database);
        var source = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Source shop");
        var target = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Target shop");
        var locked = await database.CreateTransactionAsync(-100m);
        var editable = await database.CreateTransactionAsync(-50m);
        var originalTimestamp = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await using (var context = database.CreateDbContext())
        {
            var transactions = await context.Transactions.ToListAsync();
            foreach (var transaction in transactions)
                transaction.PayeeId = source.Id;
            var reconciled = transactions.Single(transaction => transaction.Id == locked.Id);
            reconciled.IsReconciled = true;
            reconciled.UpdatedAt = originalTimestamp;
            await context.SaveChangesAsync();
        }

        Assert.IsTrue(await service.MergePayeesAsync(FinanceDatabase.Owner, target.Id, [source.Id]));

        await using var verification = database.CreateDbContext();
        var storedLocked = await verification.Transactions.SingleAsync(transaction => transaction.Id == locked.Id);
        var storedEditable = await verification.Transactions.SingleAsync(transaction => transaction.Id == editable.Id);
        Assert.AreEqual(source.Id, storedLocked.PayeeId);
        Assert.AreEqual(originalTimestamp, storedLocked.UpdatedAt);
        Assert.IsTrue(storedLocked.IsReconciled);
        Assert.AreEqual(target.Id, storedEditable.PayeeId);
        Assert.IsFalse((await verification.Payees.SingleAsync(payee => payee.Id == source.Id)).IsActive);
        Assert.IsTrue((await verification.Payees.SingleAsync(payee => payee.Id == target.Id)).IsActive);
    }

    /// <summary>Ambiguous or self-referential source sets are rejected before any source is deactivated.</summary>
    [TestMethod]
    [DataRow("empty")]
    [DataRow("duplicates")]
    [DataRow("target")]
    public async Task Invalid_source_sets_are_rejected_without_mutating_payees(string input)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new PayeeService(database);
        var source = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Source shop");
        var target = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Target shop");
        List<int> sources = input switch
        {
            "empty" => [],
            "duplicates" => [source.Id, source.Id],
            "target" => [target.Id],
            _ => throw new AssertFailedException("Unknown test input.")
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.MergePayeesAsync(FinanceDatabase.Owner, target.Id, sources));

        await using var verification = database.CreateDbContext();
        Assert.IsTrue((await verification.Payees.ToListAsync()).All(payee => payee.IsActive));
    }

    /// <summary>A foreign source cannot partially deactivate valid sources included in the same request.</summary>
    [TestMethod]
    public async Task Foreign_source_rejects_merge_without_partial_changes()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new PayeeService(database);
        var source = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Source shop");
        var target = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Target shop");
        var foreign = new Payee { UserId = FinanceDatabase.Other, Name = "Private shop" };
        await using (var context = database.CreateDbContext())
        {
            context.Payees.Add(foreign);
            await context.SaveChangesAsync();
        }

        Assert.IsFalse(await service.MergePayeesAsync(FinanceDatabase.Owner, target.Id, [source.Id, foreign.Id]));

        await using var verification = database.CreateDbContext();
        Assert.IsTrue((await verification.Payees.ToListAsync()).All(payee => payee.IsActive));
    }

    /// <summary>Archiving acts on precisely the previewed unused set and leaves foreign payees alone.</summary>
    [TestMethod]
    public async Task Exact_unused_preview_archives_only_the_owned_matching_set()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new PayeeService(database);
        var first = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "First unused");
        var second = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Second unused");
        var foreign = await service.CreateOrGetPayeeAsync(FinanceDatabase.Other, "Foreign unused");

        var count = await service.DeleteUnusedPayeesAsync(FinanceDatabase.Owner, [second.Id, first.Id]);

        Assert.AreEqual(2, count);
        await using var context = database.CreateDbContext();
        Assert.IsFalse((await context.Payees.SingleAsync(payee => payee.Id == first.Id)).IsActive);
        Assert.IsFalse((await context.Payees.SingleAsync(payee => payee.Id == second.Id)).IsActive);
        Assert.IsTrue((await context.Payees.SingleAsync(payee => payee.Id == foreign.Id)).IsActive);
    }

    /// <summary>A stale preview or a mixed-in used/foreign ID cannot archive a subset of current candidates.</summary>
    [TestMethod]
    [DataRow("missing-candidate")]
    [DataRow("foreign")]
    [DataRow("became-used")]
    public async Task Changed_or_mixed_unused_preview_rejects_without_partial_archival(string reason)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var service = new PayeeService(database);
        var first = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "First unused");
        var second = await service.CreateOrGetPayeeAsync(FinanceDatabase.Owner, "Second unused");
        var foreign = await service.CreateOrGetPayeeAsync(FinanceDatabase.Other, "Foreign unused");
        if (reason == "became-used")
        {
            var transaction = await database.CreateTransactionAsync();
            await using var context = database.CreateDbContext();
            var stored = await context.Transactions.SingleAsync(item => item.Id == transaction.Id);
            stored.PayeeId = second.Id;
            await context.SaveChangesAsync();
        }
        IReadOnlyCollection<int> expected = reason switch
        {
            "missing-candidate" => [first.Id],
            "foreign" => [first.Id, second.Id, foreign.Id],
            "became-used" => [first.Id, second.Id],
            _ => throw new AssertFailedException("Unknown test input.")
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.DeleteUnusedPayeesAsync(FinanceDatabase.Owner, expected));

        await using var verification = database.CreateDbContext();
        Assert.IsTrue((await verification.Payees.ToListAsync()).All(payee => payee.IsActive));
    }
}

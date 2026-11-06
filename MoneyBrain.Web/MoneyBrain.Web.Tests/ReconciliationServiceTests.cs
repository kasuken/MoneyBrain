using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Reconciliation;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Verifies statement selection, persisted totals and finalization against a relational database.</summary>
[TestClass]
public sealed class ReconciliationServiceTests
{
    private static readonly DateTime StatementDate = new(2026, 1, 31);

    /// <summary>Selected debits are saved before totals are queried and an exact statement can be finalized.</summary>
    [TestMethod]
    public async Task Selection_persists_reconciled_debit_and_zero_difference_then_completes()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 900m);

        Assert.IsTrue(await service.ReconcileTransactionsAsync(reconciliation.Id,
            FinanceDatabase.Owner, [transaction.Id]));

        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Reconciliations.SingleAsync();
            var selected = await context.Transactions.SingleAsync();
            Assert.AreEqual(1000m, stored.OpeningBalance);
            Assert.AreEqual(900m, stored.ReconciledBalance);
            Assert.AreEqual(0m, stored.Difference);
            Assert.IsFalse(stored.IsCompleted);
            Assert.IsTrue(selected.IsReconciled);
            Assert.IsTrue(selected.IsCleared);
            Assert.AreEqual(reconciliation.Id, selected.ReconciliationId);
            Assert.AreEqual(-100m, selected.Amount);
        }
        Assert.AreEqual(900m, await service.CalculateReconciledBalanceAsync(reconciliation.Id, FinanceDatabase.Owner));

        var completed = await service.CompleteReconciliationAsync(reconciliation.Id, FinanceDatabase.Owner);

        Assert.IsTrue(completed.IsCompleted);
        Assert.IsNotNull(completed.CompletedAt);
        await using var verification = database.CreateDbContext();
        Assert.IsTrue((await verification.Reconciliations.SingleAsync()).IsCompleted);
        Assert.IsFalse(await service.UnreconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [transaction.Id]));
        Assert.IsTrue((await verification.Transactions.AsNoTracking().SingleAsync()).IsReconciled);
    }

    /// <summary>Finalization recalculates from actual selected rows rather than trusting stale cached totals.</summary>
    [TestMethod]
    public async Task Nonzero_difference_cannot_finalize_even_when_stored_difference_is_zero()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 850m);
        await service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [transaction.Id]);
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Reconciliations.SingleAsync();
            Assert.AreEqual(-50m, stored.Difference);
            stored.Difference = 0m;
            stored.ReconciledBalance = 850m;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.CompleteReconciliationAsync(reconciliation.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        var persisted = await verification.Reconciliations.SingleAsync();
        Assert.IsFalse(persisted.IsCompleted);
        Assert.IsNull(persisted.CompletedAt);
        Assert.IsTrue((await verification.Transactions.SingleAsync()).IsReconciled);
    }

    /// <summary>Removing a selected debit updates flags and stored statement totals together.</summary>
    [TestMethod]
    public async Task Unselection_persists_restored_opening_balance_and_difference()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 900m);
        await service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [transaction.Id]);

        Assert.IsTrue(await service.UnreconcileTransactionsAsync(reconciliation.Id,
            FinanceDatabase.Owner, [transaction.Id]));

        await using var context = database.CreateDbContext();
        var stored = await context.Reconciliations.SingleAsync();
        var unselected = await context.Transactions.SingleAsync();
        Assert.AreEqual(1000m, stored.ReconciledBalance);
        Assert.AreEqual(-100m, stored.Difference);
        Assert.IsFalse(unselected.IsReconciled);
        Assert.IsNull(unselected.ReconciliationId);
    }

    /// <summary>Every requested ID must be eligible; an invalid row cannot cause partial selection of valid rows.</summary>
    [TestMethod]
    [DataRow("pending")]
    [DataRow("future")]
    [DataRow("foreign")]
    [DataRow("other-account")]
    [DataRow("missing")]
    public async Task Ineligible_selection_rejects_entire_batch_without_persisting_partial_totals(string reason)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var valid = await database.CreateTransactionAsync(-100m);
        var invalid = new Transaction
        {
            UserId = reason == "foreign" ? FinanceDatabase.Other : FinanceDatabase.Owner,
            AccountId = reason switch
            {
                "foreign" => database.ForeignAccount.Id,
                "other-account" => database.Savings.Id,
                _ => database.Checking.Id
            },
            Date = reason == "future" ? StatementDate.AddDays(1) : StatementDate,
            Status = reason == "pending" ? TransactionStatus.Pending : TransactionStatus.Posted,
            Amount = -25m
        };
        await using (var context = database.CreateDbContext())
        {
            context.Transactions.Add(invalid);
            await context.SaveChangesAsync();
        }
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 875m);
        var invalidId = reason == "missing" ? int.MaxValue : invalid.Id;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [valid.Id, invalidId]));

        await using var verification = database.CreateDbContext();
        var transactions = await verification.Transactions.ToListAsync();
        Assert.IsTrue(transactions.All(transaction => !transaction.IsReconciled && transaction.ReconciliationId is null));
        var stored = await verification.Reconciliations.SingleAsync();
        Assert.AreEqual(1000m, stored.ReconciledBalance);
        Assert.AreEqual(-125m, stored.Difference);
        Assert.IsFalse(stored.IsCompleted);
    }

    /// <summary>Completion checks selected rows again so later pending or future records cannot be locked.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Completion_rejects_selected_transaction_that_is_no_longer_eligible(bool pending)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 900m);
        await service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [transaction.Id]);
        await using (var context = database.CreateDbContext())
        {
            var selected = await context.Transactions.SingleAsync();
            selected.Status = pending ? TransactionStatus.Pending : TransactionStatus.Posted;
            selected.Date = pending ? transaction.Date : StatementDate.AddDays(1);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.CompleteReconciliationAsync(reconciliation.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        Assert.IsFalse((await verification.Reconciliations.SingleAsync()).IsCompleted);
    }

    /// <summary>Invalid unselection sets cannot change any reconciliation flag or stored total.</summary>
    [TestMethod]
    [DataRow("empty")]
    [DataRow("duplicate")]
    [DataRow("zero")]
    [DataRow("negative")]
    public async Task Invalid_unselection_ids_leave_selection_and_totals_unchanged(string input)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync(-100m);
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 900m);
        await service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [transaction.Id]);
        List<int> ids = input switch
        {
            "empty" => [],
            "duplicate" => [transaction.Id, transaction.Id],
            "zero" => [transaction.Id, 0],
            "negative" => [transaction.Id, -1],
            _ => throw new AssertFailedException("Unknown test input.")
        };

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.UnreconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, ids));

        await using var verification = database.CreateDbContext();
        var selected = await verification.Transactions.SingleAsync();
        Assert.IsTrue(selected.IsReconciled);
        Assert.AreEqual(reconciliation.Id, selected.ReconciliationId);
        var stored = await verification.Reconciliations.SingleAsync();
        Assert.AreEqual(900m, stored.ReconciledBalance);
        Assert.AreEqual(0m, stored.Difference);
        Assert.IsFalse(stored.IsCompleted);
    }

    /// <summary>Every requested ID must belong to this selection, preventing partial unselection.</summary>
    [TestMethod]
    [DataRow("missing")]
    [DataRow("unselected")]
    [DataRow("foreign")]
    [DataRow("other-reconciliation")]
    public async Task Partially_matching_unselection_rejects_entire_batch(string reason)
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var selected = await database.CreateTransactionAsync(-100m);
        var unrelated = new Transaction
        {
            UserId = reason == "foreign" ? FinanceDatabase.Other : FinanceDatabase.Owner,
            AccountId = reason == "foreign" ? database.ForeignAccount.Id : database.Checking.Id,
            Date = new DateTime(2026, 1, 15), Amount = -25m, Status = TransactionStatus.Posted
        };
        await using (var context = database.CreateDbContext())
        {
            context.Transactions.Add(unrelated);
            await context.SaveChangesAsync();
        }
        var service = new ReconciliationService(database);
        var reconciliation = await service.StartReconciliationAsync(database.Checking.Id,
            FinanceDatabase.Owner, StatementDate, 900m);
        await service.ReconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [selected.Id]);
        if (reason == "other-reconciliation")
        {
            var other = await service.StartReconciliationAsync(database.Checking.Id,
                FinanceDatabase.Owner, StatementDate, 975m);
            await service.ReconcileTransactionsAsync(other.Id, FinanceDatabase.Owner, [unrelated.Id]);
        }
        var invalidId = reason == "missing" ? int.MaxValue : unrelated.Id;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.UnreconcileTransactionsAsync(reconciliation.Id, FinanceDatabase.Owner, [selected.Id, invalidId]));

        await using var verification = database.CreateDbContext();
        var persistedSelection = await verification.Transactions.SingleAsync(transaction => transaction.Id == selected.Id);
        Assert.IsTrue(persistedSelection.IsReconciled);
        Assert.AreEqual(reconciliation.Id, persistedSelection.ReconciliationId);
        var persistedUnrelated = await verification.Transactions.SingleAsync(transaction => transaction.Id == unrelated.Id);
        Assert.AreEqual(reason == "other-reconciliation", persistedUnrelated.IsReconciled);
        Assert.AreNotEqual(reconciliation.Id, persistedUnrelated.ReconciliationId);
        var stored = await verification.Reconciliations.SingleAsync(item => item.Id == reconciliation.Id);
        Assert.AreEqual(900m, stored.ReconciledBalance);
        Assert.AreEqual(0m, stored.Difference);
        Assert.IsFalse(stored.IsCompleted);
    }
}

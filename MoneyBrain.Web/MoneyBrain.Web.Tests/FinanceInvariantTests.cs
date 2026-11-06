using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Domain.Enums;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Exercises persisted accounting invariants through the real application services.</summary>
[TestClass]
public sealed class FinanceInvariantTests
{
    /// <summary>A transfer must create two mutually linked entries with no net income or expense.</summary>
    [TestMethod]
    public async Task Transfer_creates_exactly_two_opposite_linked_transactions_and_balanced_ledger()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transfer = await database.Transfers.CreateTransferAsync(FinanceDatabase.Owner, new TransferDto
        {
            FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
            Amount = 123.45m, Date = new DateTime(2026, 1, 15)
        });

        await using var context = database.CreateDbContext();
        var transactions = await context.Transactions.ToListAsync();
        var ledger = await context.LedgerEntries.ToListAsync();
        Assert.HasCount(2, transactions);
        Assert.AreEqual(-123.45m, transfer.FromTransaction.Amount);
        Assert.AreEqual(123.45m, transfer.ToTransaction.Amount);
        Assert.AreEqual(transfer.ToTransaction.Id, transfer.FromTransaction.TransferTransactionId);
        Assert.AreEqual(transfer.FromTransaction.Id, transfer.ToTransaction.TransferTransactionId);
        Assert.IsTrue(transactions.All(transaction => transaction.CategoryId is null));
        Assert.AreEqual(0m, transactions.Sum(transaction => transaction.Amount));
        Assert.HasCount(2, ledger);
        Assert.IsTrue(ledger.All(entry => entry.CategoryId is null));
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
        var balances = new LedgerService(database);
        Assert.AreEqual(876.55m, await balances.GetAccountBalanceAsync(database.Checking.Id, FinanceDatabase.Owner));
        Assert.AreEqual(1123.45m, await balances.GetAccountBalanceAsync(database.Savings.Id, FinanceDatabase.Owner));
    }

    /// <summary>Updating a transfer changes both linked transactions, ledger dates and balances together.</summary>
    [TestMethod]
    public async Task Transfer_update_regenerates_both_ledger_entries_and_account_balances()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transfer = await database.Transfers.CreateTransferAsync(FinanceDatabase.Owner, new TransferDto
        {
            FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
            Amount = 100m, Date = new DateTime(2026, 1, 15)
        });
        var newDate = new DateTime(2026, 2, 15);

        Assert.IsTrue(await database.Transfers.UpdateTransferAsync(FinanceDatabase.Owner,
            transfer.FromTransaction.Id, new TransferDto
            {
                FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
                Amount = 250m, Date = newDate, Memo = "Updated transfer"
            }));

        await using var context = database.CreateDbContext();
        var transactions = await context.Transactions.ToListAsync();
        var ledger = await context.LedgerEntries.ToListAsync();
        Assert.HasCount(2, transactions);
        Assert.AreEqual(-250m, transactions.Single(transaction => transaction.AccountId == database.Checking.Id).Amount);
        Assert.AreEqual(250m, transactions.Single(transaction => transaction.AccountId == database.Savings.Id).Amount);
        Assert.IsTrue(transactions.All(transaction => transaction.Date == newDate && transaction.Memo == "Updated transfer"));
        Assert.HasCount(2, ledger);
        Assert.IsTrue(ledger.All(entry => entry.EntryDate == newDate && entry.CategoryId is null));
        Assert.AreEqual(250m, ledger.Sum(entry => entry.DebitAmount));
        Assert.AreEqual(250m, ledger.Sum(entry => entry.CreditAmount));
        var balances = new LedgerService(database);
        Assert.AreEqual(750m, await balances.GetAccountBalanceAsync(database.Checking.Id, FinanceDatabase.Owner));
        Assert.AreEqual(1250m, await balances.GetAccountBalanceAsync(database.Savings.Id, FinanceDatabase.Owner));
    }

    /// <summary>Deleting either transfer leg removes the whole pair and restores both account balances.</summary>
    [TestMethod]
    public async Task Transfer_delete_removes_both_transactions_and_ledger_entries()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transfer = await database.Transfers.CreateTransferAsync(FinanceDatabase.Owner, new TransferDto
        {
            FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
            Amount = 100m, Date = new DateTime(2026, 1, 15)
        });

        Assert.IsTrue(await database.Transfers.DeleteTransferAsync(FinanceDatabase.Owner, transfer.ToTransaction.Id));

        await using var context = database.CreateDbContext();
        Assert.AreEqual(0, await context.Transactions.CountAsync());
        Assert.AreEqual(0, await context.LedgerEntries.CountAsync());
        var balances = new LedgerService(database);
        Assert.AreEqual(1000m, await balances.GetAccountBalanceAsync(database.Checking.Id, FinanceDatabase.Owner));
        Assert.AreEqual(1000m, await balances.GetAccountBalanceAsync(database.Savings.Id, FinanceDatabase.Owner));
    }

    /// <summary>One reconciled leg locks the entire linked transfer against update and deletion.</summary>
    [TestMethod]
    public async Task Reconciled_transfer_leg_locks_both_transactions_and_ledger()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transfer = await database.Transfers.CreateTransferAsync(FinanceDatabase.Owner, new TransferDto
        {
            FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
            Amount = 100m, Date = new DateTime(2026, 1, 15)
        });
        await using (var context = database.CreateDbContext())
        {
            var destination = await context.Transactions.SingleAsync(transaction => transaction.Id == transfer.ToTransaction.Id);
            destination.IsReconciled = true;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transfers.UpdateTransferAsync(FinanceDatabase.Owner, transfer.FromTransaction.Id,
                new TransferDto
                {
                    FromAccountId = database.Checking.Id, ToAccountId = database.Savings.Id,
                    Amount = 250m, Date = new DateTime(2026, 2, 15)
                }));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transfers.DeleteTransferAsync(FinanceDatabase.Owner, transfer.FromTransaction.Id));

        await using var verification = database.CreateDbContext();
        var transactions = await verification.Transactions.ToListAsync();
        var ledger = await verification.LedgerEntries.ToListAsync();
        Assert.HasCount(2, transactions);
        Assert.AreEqual(-100m, transactions.Single(transaction => transaction.Id == transfer.FromTransaction.Id).Amount);
        Assert.AreEqual(100m, transactions.Single(transaction => transaction.Id == transfer.ToTransaction.Id).Amount);
        Assert.HasCount(2, ledger);
        Assert.AreEqual(100m, ledger.Sum(entry => entry.DebitAmount));
        Assert.AreEqual(100m, ledger.Sum(entry => entry.CreditAmount));
    }

    /// <summary>Ownership violations cannot leave a partial transfer in the database.</summary>
    [TestMethod]
    public async Task Transfer_to_another_users_account_is_rejected_without_writes()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transfers.CreateTransferAsync(FinanceDatabase.Owner, new TransferDto
            {
                FromAccountId = database.Checking.Id, ToAccountId = database.ForeignAccount.Id,
                Amount = 50m, Date = new DateTime(2026, 1, 15)
            }));

        await using var context = database.CreateDbContext();
        Assert.AreEqual(0, await context.Transactions.CountAsync());
        Assert.AreEqual(0, await context.LedgerEntries.CountAsync());
    }

    /// <summary>Split mismatches must fail before any parent, split, or ledger record is persisted.</summary>
    [TestMethod]
    public async Task Mismatched_split_total_is_rejected_without_writes()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
                database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
                TransactionStatus.Posted, false, null, null,
                [new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -40m }]));

        await using var context = database.CreateDbContext();
        Assert.AreEqual(0, await context.Transactions.CountAsync());
        Assert.AreEqual(0, await context.TransactionSplits.CountAsync());
        Assert.AreEqual(0, await context.LedgerEntries.CountAsync());
    }

    /// <summary>Valid split totals are persisted with their parent and balanced ledger entries.</summary>
    [TestMethod]
    public async Task Valid_splits_preserve_parent_total_and_balanced_ledger()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.Transactions.CreateTransactionWithSplitsAsync(FinanceDatabase.Owner,
            database.Checking.Id, new DateTime(2026, 1, 15), -100m, null, null,
            TransactionStatus.Posted, false, null, null,
            [
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -40m },
                new TransactionSplitDto { CategoryId = database.Category.Id, Amount = -60m }
            ]);

        await using var context = database.CreateDbContext();
        var persisted = await context.Transactions.Include(item => item.Splits).SingleAsync();
        var ledger = await context.LedgerEntries.ToListAsync();
        Assert.AreEqual(transaction.Id, persisted.Id);
        Assert.AreEqual(persisted.Amount, persisted.Splits.Sum(split => split.Amount));
        Assert.HasCount(2, persisted.Splits);
        Assert.IsTrue(ledger.Count > 0);
        Assert.AreEqual(ledger.Sum(entry => entry.DebitAmount), ledger.Sum(entry => entry.CreditAmount));
    }

    /// <summary>Reconciliation locks ordinary updates and deletes without changing stored data.</summary>
    [TestMethod]
    public async Task Reconciled_transaction_cannot_be_updated_or_deleted()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync();
        await using (var context = database.CreateDbContext())
        {
            var stored = await context.Transactions.SingleAsync();
            stored.IsReconciled = true;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.UpdateTransactionAsync(transaction.Id, FinanceDatabase.Owner,
                transaction.Date, -200m, null, database.Category.Id, "Changed", TransactionStatus.Posted,
                false, null, null));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            database.Transactions.DeleteTransactionAsync(transaction.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        var persisted = await verification.Transactions.SingleAsync();
        Assert.AreEqual(-100m, persisted.Amount);
        Assert.AreEqual("Original memo", persisted.Memo);
        Assert.IsTrue(persisted.IsReconciled);
    }

    /// <summary>A transaction identifier cannot be used to read or delete another user's entry.</summary>
    [TestMethod]
    public async Task Transaction_reads_and_deletes_are_scoped_to_owner()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync();

        Assert.IsNull(await database.Transactions.GetTransactionByIdAsync(transaction.Id, FinanceDatabase.Other));
        Assert.IsFalse(await database.Transactions.DeleteTransactionAsync(transaction.Id, FinanceDatabase.Other));
        Assert.IsEmpty(await database.Transactions.GetUserTransactionsAsync(FinanceDatabase.Other));
        Assert.IsNotNull(await database.Transactions.GetTransactionByIdAsync(transaction.Id, FinanceDatabase.Owner));
    }
}

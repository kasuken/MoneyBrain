using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Accounts;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Protects account history through the public service rather than tool-only validation.</summary>
[TestClass]
public sealed class AccountServiceTests
{
    /// <summary>Deleting an account with transactions cannot cascade away its accounting history.</summary>
    [TestMethod]
    public async Task Account_with_transaction_cannot_be_deleted_and_history_is_preserved()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        var transaction = await database.CreateTransactionAsync();
        var service = Service(database);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.DeleteAccountAsync(database.Checking.Id, FinanceDatabase.Owner));

        await using var context = database.CreateDbContext();
        Assert.IsTrue(await context.Accounts.AnyAsync(account => account.Id == database.Checking.Id));
        Assert.AreEqual(transaction.Id, (await context.Transactions.SingleAsync()).Id);
        Assert.IsTrue(await context.LedgerEntries.AnyAsync(entry => entry.TransactionId == transaction.Id));
    }

    /// <summary>Truly unused accounts can still be removed without affecting the user's other accounts.</summary>
    [TestMethod]
    public async Task Empty_owned_account_can_be_deleted()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();

        Assert.IsTrue(await Service(database).DeleteAccountAsync(database.Savings.Id, FinanceDatabase.Owner));

        await using var context = database.CreateDbContext();
        Assert.IsFalse(await context.Accounts.AnyAsync(account => account.Id == database.Savings.Id));
        Assert.IsTrue(await context.Accounts.AnyAsync(account => account.Id == database.Checking.Id));
        Assert.IsTrue(await context.Accounts.AnyAsync(account => account.Id == database.ForeignAccount.Id));
    }

    /// <summary>Knowing an account identifier never grants deletion rights to another user.</summary>
    [TestMethod]
    public async Task Foreign_account_cannot_be_deleted()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();

        Assert.IsFalse(await Service(database).DeleteAccountAsync(database.ForeignAccount.Id, FinanceDatabase.Owner));

        await using var context = database.CreateDbContext();
        Assert.IsTrue(await context.Accounts.AnyAsync(account => account.Id == database.ForeignAccount.Id));
    }

    /// <summary>Linked payment references block deletion even when the account has no transactions.</summary>
    [TestMethod]
    public async Task Referenced_payment_account_cannot_be_deleted()
    {
        await using var database = new FinanceDatabase();
        await database.InitializeAsync();
        await using (var context = database.CreateDbContext())
        {
            var linked = await context.Accounts.SingleAsync(account => account.Id == database.Savings.Id);
            linked.LinkedPaymentAccountId = database.Checking.Id;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Service(database).DeleteAccountAsync(database.Checking.Id, FinanceDatabase.Owner));

        await using var verification = database.CreateDbContext();
        Assert.AreEqual(database.Checking.Id,
            (await verification.Accounts.SingleAsync(account => account.Id == database.Savings.Id)).LinkedPaymentAccountId);
        Assert.IsTrue(await verification.Accounts.AnyAsync(account => account.Id == database.Checking.Id));
    }

    private static AccountService Service(FinanceDatabase database) => new(database,
        NullLogger<AccountService>.Instance, new LedgerService(database), database.Cache);
}

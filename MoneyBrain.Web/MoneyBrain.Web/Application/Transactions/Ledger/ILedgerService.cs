using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Transactions.Ledger;

/// <summary>
/// Service for managing double-entry bookkeeping ledger entries.
/// Ensures every transaction generates balanced debit and credit entries.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Generate ledger entries for a new transaction.
    /// Creates balanced debit/credit entries following double-entry bookkeeping rules.
    /// </summary>
    /// <param name="transaction">The transaction to generate entries for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateLedgerEntriesAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all ledger entries for a transaction (when transaction is deleted).
    /// </summary>
    /// <param name="transactionId">The transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteLedgerEntriesAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerate ledger entries for a transaction (when transaction is updated).
    /// Deletes existing entries and creates new ones.
    /// </summary>
    /// <param name="transaction">The updated transaction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RegenerateLedgerEntriesAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the account balance based on ledger entries (double-entry calculation).
    /// Balance = Opening Balance + Sum of (Debits - Credits) for Asset accounts
    /// Balance = Opening Balance + Sum of (Credits - Debits) for Liability accounts
    /// </summary>
    /// <param name="accountId">The account to calculate balance for</param>
    /// <param name="userId">The user ID</param>
    /// <param name="asOfDate">Optional date to calculate balance as of (defaults to now)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The calculated balance</returns>
    Task<decimal> GetAccountBalanceAsync(int accountId, string userId, DateTime? asOfDate = null, CancellationToken cancellationToken = default);
}

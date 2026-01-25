using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Accounts;

/// <summary>
/// Service interface for account management operations.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Get all accounts for a specific user.
    /// </summary>
    Task<IReadOnlyList<Account>> GetUserAccountsAsync(string userId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific account by ID.
    /// </summary>
    Task<Account?> GetAccountByIdAsync(int accountId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new account.
    /// </summary>
    Task<Account> CreateAccountAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing account.
    /// </summary>
    Task<Account> UpdateAccountAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete an account (set IsActive = false).
    /// </summary>
    Task<bool> DeactivateAccountAsync(int accountId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently delete an account. Only allowed if no transactions exist.
    /// </summary>
    Task<bool> DeleteAccountAsync(int accountId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjust the opening balance of an account with audit trail.
    /// </summary>
    /// <param name="accountId">Account to adjust</param>
    /// <param name="newBalance">New opening balance</param>
    /// <param name="reason">Reason for adjustment</param>
    /// <param name="userId">User making the adjustment</param>
    Task<Account> AdjustOpeningBalanceAsync(int accountId, decimal newBalance, string? reason, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all opening balance adjustments for an account.
    /// </summary>
    Task<IReadOnlyList<OpeningBalanceAdjustment>> GetOpeningBalanceAdjustmentsAsync(int accountId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a balance snapshot for an account at a specific point in time.
    /// </summary>
    /// <param name="accountId">Account to snapshot</param>
    /// <param name="balance">Calculated balance at snapshot time</param>
    /// <param name="snapshotDate">Date/time of snapshot</param>
    /// <param name="type">Type of snapshot</param>
    /// <param name="notes">Optional notes</param>
    /// <param name="userId">User creating the snapshot</param>
    Task<AccountBalanceSnapshot> CreateBalanceSnapshotAsync(int accountId, decimal balance, DateTime snapshotDate, SnapshotType type, string? notes, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get balance history snapshots for an account.
    /// </summary>
    /// <param name="accountId">Account to retrieve history for</param>
    /// <param name="userId">User requesting the history</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    Task<IReadOnlyList<AccountBalanceSnapshot>> GetBalanceHistoryAsync(int accountId, string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the current balance for an account.
    /// Balance = OpeningBalance + sum(all transactions) + adjustments.
    /// Note: In v1 without transactions, this returns the current opening balance.
    /// </summary>
    Task<decimal> CalculateCurrentBalanceAsync(int accountId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a manual balance adjustment for an account.
    /// </summary>
    /// <param name="accountId">Account to adjust</param>
    /// <param name="amount">Adjustment amount (positive = increase, negative = decrease)</param>
    /// <param name="adjustmentDate">Date when this adjustment takes effect</param>
    /// <param name="description">Required description explaining the adjustment</param>
    /// <param name="category">Optional category (e.g., "Bank Fee", "Interest")</param>
    /// <param name="userId">User creating the adjustment</param>
    Task<ManualBalanceAdjustment> CreateManualAdjustmentAsync(int accountId, decimal amount, DateTime adjustmentDate, string description, string? category, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all manual adjustments for an account.
    /// </summary>
    Task<IReadOnlyList<ManualBalanceAdjustment>> GetManualAdjustmentsAsync(int accountId, string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a manual adjustment (only if not reconciled).
    /// </summary>
    Task<bool> DeleteManualAdjustmentAsync(int adjustmentId, string userId, CancellationToken cancellationToken = default);
}

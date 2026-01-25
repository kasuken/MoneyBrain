using MoneyBrain.Web.Domain.Entities;

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
}

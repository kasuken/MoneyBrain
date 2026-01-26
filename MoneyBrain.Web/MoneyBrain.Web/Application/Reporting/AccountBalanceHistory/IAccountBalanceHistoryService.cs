namespace MoneyBrain.Web.Application.Reporting.AccountBalanceHistory;

/// <summary>
/// Service for tracking and analyzing account balance changes over time.
/// </summary>
public interface IAccountBalanceHistoryService
{
    /// <summary>
    /// Get balance history for a single account over a date range.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="accountId">Account ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="intervalDays">Interval between snapshots in days (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Account balance history</returns>
    Task<AccountBalanceHistoryDto> GetAccountBalanceHistoryAsync(
        string userId,
        int accountId,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get balance history for multiple accounts over a date range.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="accountIds">Account IDs to include (null for all accounts)</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="intervalDays">Interval between snapshots in days (default: 30)</param>
    /// <param name="includeInactive">Whether to include inactive accounts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Multi-account balance history summary</returns>
    Task<MultiAccountBalanceHistoryDto> GetMultiAccountBalanceHistoryAsync(
        string userId,
        List<int>? accountIds,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);
}

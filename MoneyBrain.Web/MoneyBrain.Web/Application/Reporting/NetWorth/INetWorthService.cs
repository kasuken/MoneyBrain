namespace MoneyBrain.Web.Application.Reporting.NetWorth;

/// <summary>
/// Service for calculating net worth (assets - liabilities) over time.
/// </summary>
public interface INetWorthService
{
    /// <summary>
    /// Get net worth history over a date range with snapshots at specified intervals.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="intervalDays">Interval between snapshots in days (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Net worth history with snapshots</returns>
    Task<NetWorthHistoryDto> GetNetWorthHistoryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get net worth snapshot at a specific date.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="asOfDate">Date to calculate net worth</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Net worth snapshot</returns>
    Task<NetWorthSnapshotDto> GetNetWorthSnapshotAsync(
        string userId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);
}

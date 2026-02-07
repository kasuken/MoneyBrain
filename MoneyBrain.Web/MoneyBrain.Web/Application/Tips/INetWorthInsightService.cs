using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for generating net worth trend insights.
/// </summary>
public interface INetWorthInsightService
{
    /// <summary>
    /// Gets net worth insights comparing current to previous period.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="asOfDate">The date to calculate net worth as of.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Net worth insights with trend analysis.</returns>
    Task<NetWorthInsightDto> GetNetWorthInsightAsync(
        string userId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets net worth insights for the current date.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current net worth insights.</returns>
    Task<NetWorthInsightDto> GetCurrentNetWorthInsightAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets net worth trend data for a specified number of months.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="months">Number of months to include in trend.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of net worth trend data points.</returns>
    Task<List<NetWorthTrendDto>> GetNetWorthTrendAsync(
        string userId,
        int months,
        CancellationToken cancellationToken = default);
}

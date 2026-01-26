namespace MoneyBrain.Web.Application.Reporting.Cashflow;

/// <summary>
/// Service for generating cashflow reports and insights.
/// </summary>
public interface ICashflowService
{
    /// <summary>
    /// Get monthly cashflow summary for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Start date (defaults to 6 months ago)</param>
    /// <param name="endDate">End date (defaults to today)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of monthly cashflow summaries</returns>
    Task<List<MonthlyCashflowDto>> GetMonthlyCashflowAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed cashflow for a specific month.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="year">Year</param>
    /// <param name="month">Month (1-12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed cashflow for the month</returns>
    Task<MonthlyCashflowDto> GetMonthCashflowAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}

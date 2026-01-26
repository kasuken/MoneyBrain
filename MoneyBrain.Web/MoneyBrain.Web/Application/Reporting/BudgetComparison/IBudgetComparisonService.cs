namespace MoneyBrain.Web.Application.Reporting.BudgetComparison;

/// <summary>
/// Service for comparing budgeted amounts against actual spending.
/// </summary>
public interface IBudgetComparisonService
{
    /// <summary>
    /// Get budget vs actual comparison summary for a date range.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Budget comparison summary</returns>
    Task<BudgetComparisonSummaryDto> GetBudgetComparisonSummaryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get budget vs actual comparison for a specific month.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="year">Year</param>
    /// <param name="month">Month (1-12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Monthly budget comparison</returns>
    Task<MonthlyBudgetComparisonDto> GetMonthlyBudgetComparisonAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}

namespace MoneyBrain.Web.Application.Reporting.CategorySpending;

/// <summary>
/// Service for generating category spending reports and analysis.
/// </summary>
public interface ICategorySpendingService
{
    /// <summary>
    /// Get category spending summary for a user over a date range.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="topCount">Number of top categories to include (default 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category spending summary</returns>
    Task<CategorySpendingSummaryDto> GetCategorySpendingSummaryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed spending data for a specific category.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="categoryId">Category ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category spending details</returns>
    Task<CategorySpendingDto?> GetCategorySpendingDetailsAsync(
        string userId,
        int categoryId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

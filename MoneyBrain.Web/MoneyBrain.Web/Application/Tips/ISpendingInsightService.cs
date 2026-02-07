using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for generating spending pattern insights.
/// </summary>
public interface ISpendingInsightService
{
    /// <summary>
    /// Gets spending insights for a specific month.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Spending insights for the specified month.</returns>
    Task<SpendingInsightDto> GetMonthlySpendingInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets spending insights comparing the current month to the previous month.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comparative spending insights.</returns>
    Task<SpendingInsightDto> GetComparativeSpendingInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}

using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for generating budget performance insights.
/// </summary>
public interface IBudgetInsightService
{
    /// <summary>
    /// Gets budget health insights for a specific month.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Budget insights for the specified month.</returns>
    Task<BudgetInsightDto> GetMonthlyBudgetInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current budget health status for the active month.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Budget insights for the current month.</returns>
    Task<BudgetInsightDto> GetCurrentBudgetHealthAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

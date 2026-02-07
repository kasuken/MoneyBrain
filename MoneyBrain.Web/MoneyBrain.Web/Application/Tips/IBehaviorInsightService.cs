using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for detecting and analyzing financial behavior patterns.
/// </summary>
public interface IBehaviorInsightService
{
    /// <summary>
    /// Gets behavioral insights for a user based on their financial patterns.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of detected behavior patterns and insights.</returns>
    Task<List<BehaviorInsightDto>> GetBehaviorInsightsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets behavior insights for a specific time period.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="startDate">The start date of the analysis period.</param>
    /// <param name="endDate">The end date of the analysis period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of behavior insights for the period.</returns>
    Task<List<BehaviorInsightDto>> GetBehaviorInsightsForPeriodAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

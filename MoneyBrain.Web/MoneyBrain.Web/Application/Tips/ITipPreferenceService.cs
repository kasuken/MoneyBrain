namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service for managing user preferences for tips and insights.
/// </summary>
public interface ITipPreferenceService
{
    /// <summary>
    /// Gets the user's tip preferences.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of preference keys and values.</returns>
    Task<Dictionary<string, bool>> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a specific tip preference.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="preferenceKey">The preference key (e.g., "ShowBudgetInsights", "ShowSpendingTips").</param>
    /// <param name="value">The preference value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully.</returns>
    Task<bool> UpdatePreferenceAsync(
        string userId,
        string preferenceKey,
        bool value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific tip category is enabled for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="category">The category to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the category is enabled.</returns>
    Task<bool> IsCategoryEnabledAsync(
        string userId,
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses a specific tip for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="tipId">The educational tip ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if dismissed successfully.</returns>
    Task<bool> DismissTipAsync(
        string userId,
        int tipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of dismissed tip IDs for the user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of dismissed tip IDs.</returns>
    Task<List<int>> GetDismissedTipIdsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

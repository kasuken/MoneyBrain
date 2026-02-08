using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for managing user tip preferences.
/// </summary>
public class TipPreferenceService : ITipPreferenceService
{
    private readonly ICacheService _cacheService;

    public TipPreferenceService(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForTipPreferences(userId);
        var cached = await _cacheService.GetAsync<Dictionary<string, bool>>(cacheKey);
        if (cached != null)
            return cached;

        // Default preferences - all tip categories enabled by default
        var preferences = new Dictionary<string, bool>
        {
            ["ShowEducationalTips"] = true,
            ["ShowBudgetInsights"] = true,
            ["ShowSpendingInsights"] = true,
            ["ShowNetWorthInsights"] = true,
            ["ShowBehaviorInsights"] = true,
            ["EnableDailyTips"] = false,
            ["EnableWeeklyDigest"] = true
        };

        // In a production system, these would be stored in a separate TipPreferences table
        // For Phase 2, we're using default preferences for all users
        // Future enhancement: Store preferences in database and query here

        await _cacheService.SetAsync(cacheKey, preferences, TimeSpan.FromHours(24));
        return preferences;
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePreferenceAsync(
        string userId,
        string preferenceKey,
        bool value,
        CancellationToken cancellationToken = default)
    {
        // In a production system, this would update the database
        // For Phase 2, we'll just invalidate the cache
        // Future enhancement: Persist to database

        // Invalidate cache
        var cacheKey = CacheKeyHelper.ForTipPreferences(userId);
        await _cacheService.RemoveAsync(cacheKey);

        return await Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<bool> IsCategoryEnabledAsync(
        string userId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var preferences = await GetPreferencesAsync(userId, cancellationToken);
        var key = $"Show{category}";
        return preferences.TryGetValue(key, out var isEnabled) && isEnabled;
    }
}

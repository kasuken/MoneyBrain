using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for managing user tip preferences.
/// </summary>
public class TipPreferenceService(
    ICacheService cacheService,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<TipPreferenceService> logger) : ITipPreferenceService
{
    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForTipPreferences(userId);
        var cached = await cacheService.GetAsync<Dictionary<string, bool>>(cacheKey);
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

        await cacheService.SetAsync(cacheKey, preferences, TimeSpan.FromHours(24));
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
        await cacheService.RemoveAsync(cacheKey);

        return true;
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

    /// <inheritdoc />
    public async Task<bool> DismissTipAsync(
        string userId,
        int tipId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            // Check if preference already exists
            var existing = await context.UserTipPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.EducationalTipId == tipId, cancellationToken);

            if (existing != null)
            {
                // Update existing preference
                existing.IsDismissed = true;
                existing.DismissedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new preference
                var preference = new UserTipPreference
                {
                    UserId = userId,
                    EducationalTipId = tipId,
                    IsDismissed = true,
                    DismissedAt = DateTime.UtcNow
                };
                context.UserTipPreferences.Add(preference);
            }

            await context.SaveChangesAsync(cancellationToken);

            // Invalidate cache to reflect the change
            var cacheKey = CacheKeyHelper.ForTipPreferences(userId);
            await cacheService.RemoveAsync(cacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dismissing tip {TipId} for user {UserId}", tipId, userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<List<int>> GetDismissedTipIdsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.UserTipPreferences
            .Where(p => p.UserId == userId && p.IsDismissed)
            .Select(p => p.EducationalTipId)
            .ToListAsync(cancellationToken);
    }
}

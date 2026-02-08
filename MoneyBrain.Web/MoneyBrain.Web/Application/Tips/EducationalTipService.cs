using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for managing educational financial tips.
/// </summary>
public class EducationalTipService : IEducationalTipService
{
    private readonly ICacheService _cacheService;

    public EducationalTipService(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<List<EducationalTipDto>> GetActiveTipsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForEducationalTips(userId);
        var cached = await _cacheService.GetAsync<List<EducationalTipDto>>(cacheKey);
        if (cached != null)
            return cached;

        // For now, return a curated list of educational tips
        // In a production system, this would query a Tips table in the database
        var tips = new List<EducationalTipDto>
        {
            new()
            {
                Id = 1,
                Title = "Track Every Expense",
                Description = "Recording all expenses, even small ones, helps identify spending patterns and areas for improvement. Consider using categories to organize your spending.",
                Category = "Budgeting",
                Priority = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Tags = ["budgeting", "tracking", "basics"],
                ResourceUrl = null
            },
            new()
            {
                Id = 2,
                Title = "Build an Emergency Fund",
                Description = "Set aside 3-6 months of expenses in a separate savings account. This provides financial security and reduces stress during unexpected events.",
                Category = "Saving",
                Priority = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Tags = ["saving", "emergency", "security"],
                ResourceUrl = null
            },
            new()
            {
                Id = 3,
                Title = "Review Subscriptions Monthly",
                Description = "Recurring subscriptions can add up quickly. Review them monthly to cancel unused services and save money.",
                Category = "Spending",
                Priority = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Tags = ["spending", "subscriptions", "saving"],
                ResourceUrl = null
            },
            new()
            {
                Id = 4,
                Title = "Use the 50/30/20 Budget Rule",
                Description = "Allocate 50% of income to needs, 30% to wants, and 20% to savings and debt repayment. This provides a balanced approach to money management.",
                Category = "Budgeting",
                Priority = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Tags = ["budgeting", "allocation", "strategy"],
                ResourceUrl = null
            },
            new()
            {
                Id = 5,
                Title = "Automate Savings",
                Description = "Set up automatic transfers to savings accounts on payday. This ensures consistent saving without requiring willpower.",
                Category = "Saving",
                Priority = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Tags = ["saving", "automation", "consistency"],
                ResourceUrl = null
            }
        };

        await _cacheService.SetAsync(cacheKey, tips, TimeSpan.FromHours(24));
        return tips;
    }

    /// <inheritdoc />
    public async Task<List<EducationalTipDto>> GetTipsByCategoryAsync(
        string userId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var allTips = await GetActiveTipsAsync(userId, cancellationToken);
        return allTips
            .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<EducationalTipDto?> GetTipByIdAsync(
        string userId,
        int tipId,
        CancellationToken cancellationToken = default)
    {
        var allTips = await GetActiveTipsAsync(userId, cancellationToken);
        return allTips.FirstOrDefault(t => t.Id == tipId);
    }
}

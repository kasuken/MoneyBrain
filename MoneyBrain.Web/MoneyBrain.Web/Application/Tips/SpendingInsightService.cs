using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Reporting.CategorySpending;
using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for generating spending pattern insights.
/// </summary>
public class SpendingInsightService(
    ICacheService cacheService,
    ICategorySpendingService categorySpendingService) : ISpendingInsightService
{
    /// <inheritdoc />
    public async Task<SpendingInsightDto> GetMonthlySpendingInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForSpendingInsight(userId, year, month);
        var cached = await cacheService.GetAsync<SpendingInsightDto>(cacheKey);
        if (cached != null)
            return cached;

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var categorySpendingSummary = await categorySpendingService.GetCategorySpendingSummaryAsync(
            userId,
            startDate,
            endDate,
            cancellationToken: cancellationToken);

        var totalSpending = categorySpendingSummary.TotalSpending;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var averageDaily = totalSpending / daysInMonth;

        var topCategory = categorySpendingSummary.TopCategories.FirstOrDefault();
        var period = startDate.ToString("MMMM yyyy");

        var insight = new SpendingInsightDto
        {
            Message = GenerateSpendingMessage(totalSpending, topCategory?.CategoryName ?? "Unknown"),
            Period = period,
            TotalSpending = totalSpending,
            AverageDailySpending = averageDaily,
            TopSpendingCategory = topCategory?.CategoryName ?? "None",
            TopCategoryAmount = topCategory?.TotalSpending ?? 0,
            CategoryComparisons = [],
            GeneratedAt = DateTime.UtcNow
        };

        await cacheService.SetAsync(cacheKey, insight, TimeSpan.FromHours(1));
        return insight;
    }

    /// <inheritdoc />
    public async Task<SpendingInsightDto> GetComparativeSpendingInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForComparativeSpendingInsight(userId, year, month);
        var cached = await cacheService.GetAsync<SpendingInsightDto>(cacheKey);
        if (cached != null)
            return cached;

        // Get current month spending
        var currentDate = new DateTime(year, month, 1);
        var currentEndDate = currentDate.AddMonths(1).AddDays(-1);
        var currentSpendingSummary = await categorySpendingService.GetCategorySpendingSummaryAsync(
            userId,
            currentDate,
            currentEndDate,
            cancellationToken: cancellationToken);

        // Get previous month spending
        var previousDate = currentDate.AddMonths(-1);
        var previousEndDate = previousDate.AddMonths(1).AddDays(-1);
        var previousSpendingSummary = await categorySpendingService.GetCategorySpendingSummaryAsync(
            userId,
            previousDate,
            previousEndDate,
            cancellationToken: cancellationToken);

        var totalCurrent = currentSpendingSummary.TotalSpending;
        var totalPrevious = previousSpendingSummary.TotalSpending;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var averageDaily = totalCurrent / daysInMonth;

        var topCategory = currentSpendingSummary.TopCategories.FirstOrDefault();

        var previousByCategory = previousSpendingSummary.Categories
            .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().TotalSpending, StringComparer.OrdinalIgnoreCase);

        // Build category comparisons
        var comparisons = new List<CategorySpendingComparisonDto>();
        foreach (var current in currentSpendingSummary.Categories)
        {
            previousByCategory.TryGetValue(current.CategoryName, out var previousAmount);
            var percentageChange = previousAmount != 0
                ? ((current.TotalSpending - previousAmount) / previousAmount) * 100
                : (current.TotalSpending > 0 ? 100 : 0);

            comparisons.Add(new CategorySpendingComparisonDto
            {
                CategoryName = current.CategoryName,
                CurrentAmount = current.TotalSpending,
                PreviousAmount = previousAmount,
                PercentageChange = percentageChange
            });
        }

        var period = currentDate.ToString("MMMM yyyy");

        var insight = new SpendingInsightDto
        {
            Message = GenerateComparativeMessage(totalCurrent, totalPrevious, topCategory?.CategoryName ?? "Unknown"),
            Period = period,
            TotalSpending = totalCurrent,
            AverageDailySpending = averageDaily,
            TopSpendingCategory = topCategory?.CategoryName ?? "None",
            TopCategoryAmount = topCategory?.TotalSpending ?? 0,
            CategoryComparisons = comparisons,
            GeneratedAt = DateTime.UtcNow
        };

        await cacheService.SetAsync(cacheKey, insight, TimeSpan.FromHours(1));
        return insight;
    }

    private static string GenerateSpendingMessage(decimal totalSpending, string topCategory)
    {
        if (totalSpending == 0)
            return "No spending recorded for this period.";

        return $"Total spending was {totalSpending:C}. The largest category was {topCategory}.";
    }

    private static string GenerateComparativeMessage(decimal current, decimal previous, string topCategory)
    {
        if (current == 0)
            return "No spending recorded for this period.";

        if (previous == 0)
            return $"Spending was {current:C} this month. The largest category was {topCategory}.";

        var change = current - previous;
        var percentChange = (change / previous) * 100;
        var direction = change > 0 ? "increased" : "decreased";

        return $"Spending {direction} by {Math.Abs(percentChange):F1}% compared to last month, totaling {current:C}. The largest category was {topCategory}.";
    }
}

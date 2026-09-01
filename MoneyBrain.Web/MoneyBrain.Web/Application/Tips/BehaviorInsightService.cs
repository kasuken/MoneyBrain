using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Reporting.CategorySpending;
using MoneyBrain.Web.Application.Tips.DTOs;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for detecting and analyzing financial behavior patterns.
/// </summary>
public class BehaviorInsightService : IBehaviorInsightService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ICategorySpendingService _categorySpendingService;

    public BehaviorInsightService(
        ApplicationDbContext context,
        ICacheService cacheService,
        ICategorySpendingService categorySpendingService)
    {
        _context = context;
        _cacheService = cacheService;
        _categorySpendingService = categorySpendingService;
    }

    /// <inheritdoc />
    public async Task<List<BehaviorInsightDto>> GetBehaviorInsightsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-90); // Analyze last 90 days
        return await GetBehaviorInsightsForPeriodAsync(userId, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<BehaviorInsightDto>> GetBehaviorInsightsForPeriodAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForBehaviorInsights(userId, startDate, endDate);
        var cached = await _cacheService.GetAsync<List<BehaviorInsightDto>>(cacheKey);
        if (cached != null)
            return cached;

        var insights = new List<BehaviorInsightDto>();

        // Analyze spending patterns
        await AnalyzeSpendingPatternsAsync(userId, startDate, endDate, insights, cancellationToken);

        // Analyze transaction frequency
        await AnalyzeTransactionFrequencyAsync(userId, startDate, endDate, insights, cancellationToken);

        // Sort by severity (highest first)
        insights = insights.OrderByDescending(i => i.Severity).ToList();

        await _cacheService.SetAsync(cacheKey, insights, TimeSpan.FromHours(1));
        return insights;
    }

    private async Task AnalyzeSpendingPatternsAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        List<BehaviorInsightDto> insights,
        CancellationToken cancellationToken)
    {
        // Get spending for each month in the period
        var monthlySpending = new List<decimal>();
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (currentDate <= endMonth)
        {
            var monthEndDate = currentDate.AddMonths(1).AddDays(-1);
            var summary = await _categorySpendingService.GetCategorySpendingSummaryAsync(
                userId,
                currentDate,
                monthEndDate,
                cancellationToken: cancellationToken);
            monthlySpending.Add(summary.TotalSpending);
            currentDate = currentDate.AddMonths(1);
        }

        if (monthlySpending.Count >= 2)
        {
            var average = monthlySpending.Average();
            var lastMonth = monthlySpending.Last();
            var variance = lastMonth - average;
            var variancePercent = average > 0 ? (variance / average) * 100 : 0;

            // Check for consistent overspending
            if (variancePercent > 20)
            {
                insights.Add(new BehaviorInsightDto
                {
                    Message = $"Recent spending is {variancePercent:F1}% above the average for this period.",
                    BehaviorType = "Spending Pattern",
                    Severity = 4,
                    Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}",
                    PatternDescription = "Spending has increased significantly compared to the average.",
                    SuggestedActions =
                    [
                        "Review recent transactions for unusual expenses",
                        "Check if any large one-time purchases occurred",
                        "Consider adjusting next month's budget if the trend continues"
                    ],
                    IsPositive = false,
                    Metrics = new Dictionary<string, decimal>
                    {
                        ["AverageSpending"] = average,
                        ["LastMonthSpending"] = lastMonth,
                        ["VariancePercent"] = variancePercent
                    },
                    GeneratedAt = DateTime.UtcNow
                });
            }
            else if (variancePercent < -15)
            {
                insights.Add(new BehaviorInsightDto
                {
                    Message = $"Recent spending is {Math.Abs(variancePercent):F1}% below the average for this period.",
                    BehaviorType = "Spending Pattern",
                    Severity = 2,
                    Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}",
                    PatternDescription = "Spending has decreased compared to the average.",
                    SuggestedActions =
                    [
                        "This could indicate successful budget management",
                        "Consider allocating the saved amount to savings or debt repayment"
                    ],
                    IsPositive = true,
                    Metrics = new Dictionary<string, decimal>
                    {
                        ["AverageSpending"] = average,
                        ["LastMonthSpending"] = lastMonth,
                        ["VariancePercent"] = variancePercent
                    },
                    GeneratedAt = DateTime.UtcNow
                });
            }
        }
    }

    private async Task AnalyzeTransactionFrequencyAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        List<BehaviorInsightDto> insights,
        CancellationToken cancellationToken)
    {
        var transactionCount = await _context.LedgerEntries
            .Where(le => le.UserId == userId &&
                         le.EntryDate >= startDate &&
                         le.EntryDate <= endDate &&
                         le.DebitAmount > 0) // Debit amounts represent expenses in the ledger
            .CountAsync(cancellationToken);

        var days = (endDate - startDate).Days;
        if (days > 0)
        {
            var avgTransactionsPerDay = (decimal)transactionCount / days;

            if (avgTransactionsPerDay > 5)
            {
                insights.Add(new BehaviorInsightDto
                {
                    Message = $"An average of {avgTransactionsPerDay:F1} expense transactions per day was recorded.",
                    BehaviorType = "Transaction Frequency",
                    Severity = 3,
                    Period = $"{startDate:MMM yyyy} - {endDate:MMM yyyy}",
                    PatternDescription = "High transaction frequency may indicate frequent small purchases.",
                    SuggestedActions =
                    [
                        "Consider consolidating purchases to reduce transaction fees",
                        "Review if frequent small purchases are necessary",
                        "Track categories with the most frequent transactions"
                    ],
                    IsPositive = false,
                    Metrics = new Dictionary<string, decimal>
                    {
                        ["TotalTransactions"] = transactionCount,
                        ["AveragePerDay"] = avgTransactionsPerDay,
                        ["Days"] = days
                    },
                    GeneratedAt = DateTime.UtcNow
                });
            }
        }
    }
}

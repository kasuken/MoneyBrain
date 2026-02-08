using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Reporting.BudgetComparison;
using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for generating budget performance insights.
/// </summary>
public class BudgetInsightService : IBudgetInsightService
{
    private readonly ICacheService _cacheService;
    private readonly IBudgetComparisonService _budgetComparisonService;

    public BudgetInsightService(
        ICacheService cacheService,
        IBudgetComparisonService budgetComparisonService)
    {
        _cacheService = cacheService;
        _budgetComparisonService = budgetComparisonService;
    }

    /// <inheritdoc />
    public async Task<BudgetInsightDto> GetMonthlyBudgetInsightAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForBudgetInsight(userId, year, month);
        var cached = await _cacheService.GetAsync<BudgetInsightDto>(cacheKey);
        if (cached != null)
            return cached;

        var budgetComparison = await _budgetComparisonService.GetMonthlyBudgetComparisonAsync(
            userId,
            year,
            month,
            cancellationToken);

        var categoryAnalysis = new List<CategoryBudgetAnalysisDto>();
        var categoriesOverBudget = 0;

        foreach (var category in budgetComparison.Categories)
        {
            var isOverBudget = category.Actual > category.Budgeted;
            if (isOverBudget)
                categoriesOverBudget++;

            categoryAnalysis.Add(new CategoryBudgetAnalysisDto
            {
                CategoryName = category.CategoryName,
                Budgeted = category.Budgeted,
                Actual = category.Actual
            });
        }

        var utilization = budgetComparison.TotalBudgeted > 0
            ? (budgetComparison.TotalActual / budgetComparison.TotalBudgeted) * 100
            : 0;

        var healthStatus = DetermineHealthStatus(utilization);

        var insight = new BudgetInsightDto
        {
            Message = GenerateBudgetMessage(budgetComparison.TotalBudgeted, budgetComparison.TotalActual, healthStatus),
            Period = new DateTime(year, month, 1).ToString("MMMM yyyy"),
            TotalBudgeted = budgetComparison.TotalBudgeted,
            TotalActual = budgetComparison.TotalActual,
            HealthStatus = healthStatus,
            CategoryAnalysis = categoryAnalysis,
            CategoriesOverBudget = categoriesOverBudget,
            GeneratedAt = DateTime.UtcNow
        };

        await _cacheService.SetAsync(cacheKey, insight, TimeSpan.FromHours(1));
        return insight;
    }

    /// <inheritdoc />
    public async Task<BudgetInsightDto> GetCurrentBudgetHealthAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await GetMonthlyBudgetInsightAsync(userId, now.Year, now.Month, cancellationToken);
    }

    private static BudgetHealthStatus DetermineHealthStatus(decimal utilizationPercentage)
    {
        return utilizationPercentage switch
        {
            > 100 => BudgetHealthStatus.OverBudget,
            >= 70 => BudgetHealthStatus.NeedsAttention,
            _ => BudgetHealthStatus.Healthy
        };
    }

    private static string GenerateBudgetMessage(decimal budgeted, decimal actual, BudgetHealthStatus status)
    {
        if (budgeted == 0)
            return "No budget has been set for this period.";

        var remaining = budgeted - actual;
        var utilizationPct = (actual / budgeted) * 100;

        return status switch
        {
            BudgetHealthStatus.Healthy => 
                $"Budget is on track with {utilizationPct:F1}% utilized. {remaining:C} remains available.",
            BudgetHealthStatus.NeedsAttention => 
                $"Budget utilization is at {utilizationPct:F1}%. {remaining:C} remains. Consider monitoring spending closely.",
            BudgetHealthStatus.OverBudget => 
                $"Budget has been exceeded by {Math.Abs(remaining):C} ({utilizationPct:F1}% utilized).",
            _ => "Budget status could not be determined."
        };
    }
}

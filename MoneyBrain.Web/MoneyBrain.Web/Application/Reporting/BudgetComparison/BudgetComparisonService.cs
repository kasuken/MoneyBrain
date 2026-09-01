using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Budgets;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Reporting.BudgetComparison;

/// <summary>
/// Service for comparing budgeted amounts against actual spending from ledger entries.
/// </summary>
public class BudgetComparisonService : IBudgetComparisonService
{
    private readonly ApplicationDbContext _context;
    private readonly IBudgetService _budgetService;
    private readonly ICacheService _cacheService;

    public BudgetComparisonService(ApplicationDbContext context, IBudgetService budgetService, ICacheService cacheService)
    {
        _context = context;
        _budgetService = budgetService;
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<BudgetComparisonSummaryDto> GetBudgetComparisonSummaryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var monthlyComparisons = new List<MonthlyBudgetComparisonDto>();

        // Generate list of months in the date range
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
        var endDateMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (currentDate <= endDateMonth)
        {
            var monthlyComparison = await GetMonthlyBudgetComparisonAsync(
                userId, 
                currentDate.Year, 
                currentDate.Month, 
                cancellationToken);

            monthlyComparisons.Add(monthlyComparison);
            currentDate = currentDate.AddMonths(1);
        }

        return new BudgetComparisonSummaryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalBudgeted = monthlyComparisons.Sum(m => m.TotalBudgeted),
            TotalActual = monthlyComparisons.Sum(m => m.TotalActual),
            MonthlyComparisons = monthlyComparisons.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetComparisonDto> GetMonthlyBudgetComparisonAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForBudgetComparison(userId, year, month);
        var cached = await _cacheService.GetAsync<MonthlyBudgetComparisonDto>(cacheKey);
        if (cached != null)
            return cached;

        // Get all budgets for this month (period-specific or default)
        var budgetsForPeriod = await _budgetService.GetBudgetsForPeriodAsync(userId, year, month, cancellationToken);
        var defaultBudgets = await _budgetService.GetDefaultBudgetsAsync(userId, cancellationToken);
        
        // Combine: period-specific budgets take precedence, fall back to defaults
        var allBudgets = budgetsForPeriod.Any() ? budgetsForPeriod : defaultBudgets;
        
        // Flatten all budget categories from all budgets
        var budgetedCategories = allBudgets
            .SelectMany(b => b.BudgetCategories)
            .GroupBy(bc => bc.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Category = g.First().Category,
                PlannedAmount = g.Sum(bc => bc.PlannedAmount)
            })
            .ToList();

        // Get actual spending from ledger entries for this month
        var firstDayOfMonth = new DateTime(year, month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        var actualSpending = await _context.LedgerEntries
            .AsNoTracking()
            .Where(le => le.UserId == userId 
                         && le.CategoryId != null 
                         && le.DebitAmount > 0 // Expenses are debits
                         && le.EntryDate >= firstDayOfMonth 
                         && le.EntryDate <= lastDayOfMonth)
            .Include(le => le.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .GroupBy(le => new 
            { 
                CategoryId = le.CategoryId!.Value, 
                CategoryName = le.Category!.Name,
                CategoryGroupName = le.Category.CategoryGroup.Name
            })
            .Select(g => new 
            { 
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategoryGroupName,
                TotalSpent = g.Sum(le => le.DebitAmount),
                TransactionCount = g.Select(le => le.TransactionId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var actualSpendingByCategoryId = actualSpending
            .ToDictionary(a => a.CategoryId);

        // Build category comparisons
        var categoryComparisons = new List<CategoryBudgetComparisonDto>(budgetedCategories.Count);

        // Only show categories that have a budget defined (exclude unbudgeted spending)
        foreach (var budgetedCategory in budgetedCategories)
        {
            actualSpendingByCategoryId.TryGetValue(budgetedCategory.CategoryId, out var spending);

            categoryComparisons.Add(new CategoryBudgetComparisonDto
            {
                CategoryId = budgetedCategory.CategoryId,
                CategoryName = budgetedCategory.Category?.Name ?? "Unknown",
                CategoryGroupName = budgetedCategory.Category?.CategoryGroup?.Name,
                Budgeted = budgetedCategory.PlannedAmount,
                Actual = spending?.TotalSpent ?? 0,
                TransactionCount = spending?.TransactionCount ?? 0
            });
        }

        // Sort by budgeted amount descending
        categoryComparisons = categoryComparisons
            .OrderByDescending(c => c.Budgeted)
            .ThenBy(c => c.CategoryName)
            .ToList();

        var result = new MonthlyBudgetComparisonDto
        {
            Year = year,
            Month = month,
            TotalBudgeted = categoryComparisons.Sum(c => c.Budgeted),
            TotalActual = categoryComparisons.Sum(c => c.Actual),
            Categories = categoryComparisons
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));

        return result;
    }
}

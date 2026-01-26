using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Categories;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Reporting.BudgetComparison;

/// <summary>
/// Service for comparing budgeted amounts against actual spending from ledger entries.
/// </summary>
public class BudgetComparisonService : IBudgetComparisonService
{
    private readonly ApplicationDbContext _context;
    private readonly ICategoryService _categoryService;

    public BudgetComparisonService(ApplicationDbContext context, ICategoryService categoryService)
    {
        _context = context;
        _categoryService = categoryService;
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
        // Get effective budgets for this month
        var effectiveBudgets = await _categoryService.GetEffectiveBudgetsForMonthAsync(userId, year, month, cancellationToken);

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

        // Build category comparisons
        var categoryComparisons = new List<CategoryBudgetComparisonDto>();

        // Get all categories that have either a budget or actual spending
        var categoriesWithBudget = effectiveBudgets.Select(b => b.CategoryId).ToHashSet();
        var categoriesWithSpending = actualSpending.Select(a => a.CategoryId).ToHashSet();
        var allCategories = categoriesWithBudget.Union(categoriesWithSpending).ToHashSet();

        foreach (var categoryId in allCategories)
        {
            var budget = effectiveBudgets.FirstOrDefault(b => b.CategoryId == categoryId);
            var spending = actualSpending.FirstOrDefault(a => a.CategoryId == categoryId);

            categoryComparisons.Add(new CategoryBudgetComparisonDto
            {
                CategoryId = categoryId,
                CategoryName = budget?.Category?.Name ?? spending?.CategoryName ?? "Unknown",
                CategoryGroupName = budget?.Category?.CategoryGroup?.Name ?? spending?.CategoryGroupName,
                Budgeted = budget?.PlannedAmount ?? 0,
                Actual = spending?.TotalSpent ?? 0,
                TransactionCount = spending?.TransactionCount ?? 0
            });
        }

        // Sort by budgeted amount descending
        categoryComparisons = categoryComparisons
            .OrderByDescending(c => c.Budgeted)
            .ThenBy(c => c.CategoryName)
            .ToList();

        return new MonthlyBudgetComparisonDto
        {
            Year = year,
            Month = month,
            TotalBudgeted = categoryComparisons.Sum(c => c.Budgeted),
            TotalActual = categoryComparisons.Sum(c => c.Actual),
            Categories = categoryComparisons
        };
    }
}

namespace MoneyBrain.Web.Application.Reporting.BudgetComparison;

/// <summary>
/// Represents budget vs actual comparison for a specific month.
/// </summary>
public class MonthlyBudgetComparisonDto
{
    /// <summary>
    /// Year of the budget period.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month of the budget period (1-12).
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Total budgeted amount for the month.
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    /// <summary>
    /// Total actual spending for the month.
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Remaining amount (budgeted - actual). Positive means under budget.
    /// </summary>
    public decimal Remaining => TotalBudgeted - TotalActual;

    /// <summary>
    /// Percentage of budget used.
    /// </summary>
    public decimal PercentageUsed => TotalBudgeted > 0 ? (TotalActual / TotalBudgeted * 100) : 0;

    /// <summary>
    /// Whether spending exceeded budget.
    /// </summary>
    public bool IsOverBudget => TotalActual > TotalBudgeted;

    /// <summary>
    /// Category-level budget vs actual comparison.
    /// </summary>
    public List<CategoryBudgetComparisonDto> Categories { get; set; } = [];

    /// <summary>
    /// Number of categories tracked.
    /// </summary>
    public int CategoryCount => Categories.Count;

    /// <summary>
    /// Number of categories over budget.
    /// </summary>
    public int CategoriesOverBudget => Categories.Count(c => c.IsOverBudget);

    /// <summary>
    /// Month display name.
    /// </summary>
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
}

/// <summary>
/// Represents budget vs actual comparison for a specific category.
/// </summary>
public class CategoryBudgetComparisonDto
{
    /// <summary>
    /// Category ID.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Category name.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Category group name.
    /// </summary>
    public string? CategoryGroupName { get; set; }

    /// <summary>
    /// Budgeted amount for this category.
    /// </summary>
    public decimal Budgeted { get; set; }

    /// <summary>
    /// Actual spending in this category.
    /// </summary>
    public decimal Actual { get; set; }

    /// <summary>
    /// Remaining amount (budgeted - actual). Positive means under budget.
    /// </summary>
    public decimal Remaining => Budgeted - Actual;

    /// <summary>
    /// Percentage of budget used.
    /// </summary>
    public decimal PercentageUsed => Budgeted > 0 ? (Actual / Budgeted * 100) : 0;

    /// <summary>
    /// Whether spending exceeded budget for this category.
    /// </summary>
    public bool IsOverBudget => Actual > Budgeted;

    /// <summary>
    /// Number of transactions in this category.
    /// </summary>
    public int TransactionCount { get; set; }
}

/// <summary>
/// Summary of budget vs actual comparison across multiple months.
/// </summary>
public class BudgetComparisonSummaryDto
{
    /// <summary>
    /// Start date of the reporting period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the reporting period.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total budgeted amount across all months.
    /// </summary>
    public decimal TotalBudgeted { get; set; }

    /// <summary>
    /// Total actual spending across all months.
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Total remaining (budgeted - actual).
    /// </summary>
    public decimal TotalRemaining => TotalBudgeted - TotalActual;

    /// <summary>
    /// Overall percentage of budget used.
    /// </summary>
    public decimal OverallPercentageUsed => TotalBudgeted > 0 ? (TotalActual / TotalBudgeted * 100) : 0;

    /// <summary>
    /// Monthly budget comparisons.
    /// </summary>
    public List<MonthlyBudgetComparisonDto> MonthlyComparisons { get; set; } = [];

    /// <summary>
    /// Number of months analyzed.
    /// </summary>
    public int MonthCount => MonthlyComparisons.Count;

    /// <summary>
    /// Number of months over budget.
    /// </summary>
    public int MonthsOverBudget => MonthlyComparisons.Count(m => m.IsOverBudget);
}

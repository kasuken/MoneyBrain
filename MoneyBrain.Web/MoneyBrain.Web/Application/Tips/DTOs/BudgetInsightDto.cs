namespace MoneyBrain.Web.Application.Tips.DTOs;

/// <summary>
/// Represents budget performance and health insights.
/// </summary>
public record BudgetInsightDto
{
    /// <summary>
    /// Gets the insight message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the budget month (e.g., "January 2024").
    /// </summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total budgeted amount.
    /// </summary>
    public decimal TotalBudgeted { get; init; }

    /// <summary>
    /// Gets the total actual spending.
    /// </summary>
    public decimal TotalActual { get; init; }

    /// <summary>
    /// Gets the remaining budget amount.
    /// </summary>
    public decimal Remaining => TotalBudgeted - TotalActual;

    /// <summary>
    /// Gets the budget utilization percentage.
    /// </summary>
    public decimal UtilizationPercentage => TotalBudgeted > 0 ? (TotalActual / TotalBudgeted) * 100 : 0;

    /// <summary>
    /// Gets the overall budget health status.
    /// </summary>
    public BudgetHealthStatus HealthStatus { get; init; }

    /// <summary>
    /// Gets detailed analysis per category.
    /// </summary>
    public List<CategoryBudgetAnalysisDto> CategoryAnalysis { get; init; } = [];

    /// <summary>
    /// Gets the number of categories over budget.
    /// </summary>
    public int CategoriesOverBudget { get; init; }

    /// <summary>
    /// Gets the date when this insight was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Represents budget analysis for a specific category.
/// </summary>
public record CategoryBudgetAnalysisDto
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the budgeted amount for this category.
    /// </summary>
    public decimal Budgeted { get; init; }

    /// <summary>
    /// Gets the actual spending for this category.
    /// </summary>
    public decimal Actual { get; init; }

    /// <summary>
    /// Gets the remaining amount.
    /// </summary>
    public decimal Remaining => Budgeted - Actual;

    /// <summary>
    /// Gets the utilization percentage.
    /// </summary>
    public decimal UtilizationPercentage => Budgeted > 0 ? (Actual / Budgeted) * 100 : 0;

    /// <summary>
    /// Gets a value indicating whether the category is over budget.
    /// </summary>
    public bool IsOverBudget => Actual > Budgeted;

    /// <summary>
    /// Gets a value indicating whether the category is at risk (>80% utilized).
    /// </summary>
    public bool IsAtRisk => UtilizationPercentage >= 80 && !IsOverBudget;
}

/// <summary>
/// Enumeration of budget health status levels.
/// </summary>
public enum BudgetHealthStatus
{
    /// <summary>
    /// Budget is healthy (under 70% utilization).
    /// </summary>
    Healthy,

    /// <summary>
    /// Budget needs monitoring (70-100% utilization).
    /// </summary>
    NeedsAttention,

    /// <summary>
    /// Budget is over limit (>100% utilization).
    /// </summary>
    OverBudget
}

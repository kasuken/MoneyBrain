namespace MoneyBrain.Web.Application.Tips.DTOs;

/// <summary>
/// Represents a spending analysis insight for the user.
/// </summary>
public record SpendingInsightDto
{
    /// <summary>
    /// Gets the insight message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the time period analyzed (e.g., "January 2024", "Last 30 days").
    /// </summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total spending amount for the period.
    /// </summary>
    public decimal TotalSpending { get; init; }

    /// <summary>
    /// Gets the average daily spending.
    /// </summary>
    public decimal AverageDailySpending { get; init; }

    /// <summary>
    /// Gets the category with the highest spending.
    /// </summary>
    public string TopSpendingCategory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the amount spent in the top category.
    /// </summary>
    public decimal TopCategoryAmount { get; init; }

    /// <summary>
    /// Gets percentage comparisons across categories.
    /// </summary>
    public List<CategorySpendingComparisonDto> CategoryComparisons { get; init; } = [];

    /// <summary>
    /// Gets the date when this insight was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Represents spending comparison for a specific category.
/// </summary>
public record CategorySpendingComparisonDto
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current period spending.
    /// </summary>
    public decimal CurrentAmount { get; init; }

    /// <summary>
    /// Gets the previous period spending.
    /// </summary>
    public decimal PreviousAmount { get; init; }

    /// <summary>
    /// Gets the percentage change from previous period.
    /// </summary>
    public decimal PercentageChange { get; init; }

    /// <summary>
    /// Gets the absolute change amount.
    /// </summary>
    public decimal AbsoluteChange => CurrentAmount - PreviousAmount;

    /// <summary>
    /// Gets a value indicating whether spending increased.
    /// </summary>
    public bool IsIncrease => CurrentAmount > PreviousAmount;
}

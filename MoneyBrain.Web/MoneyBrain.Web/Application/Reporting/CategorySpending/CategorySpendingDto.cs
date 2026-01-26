namespace MoneyBrain.Web.Application.Reporting.CategorySpending;

/// <summary>
/// Represents spending data for a specific category over a time period.
/// </summary>
public class CategorySpendingDto
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
    /// Total spending in this category for the period.
    /// </summary>
    public decimal TotalSpending { get; set; }

    /// <summary>
    /// Number of transactions in this category.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Average transaction amount.
    /// </summary>
    public decimal AverageTransactionAmount => TransactionCount > 0 ? TotalSpending / TransactionCount : 0;

    /// <summary>
    /// Monthly spending breakdown for this category.
    /// </summary>
    public List<MonthlySpendingDto> MonthlyBreakdown { get; set; } = [];

    /// <summary>
    /// Percentage of total spending across all categories.
    /// </summary>
    public decimal PercentageOfTotal { get; set; }
}

/// <summary>
/// Represents spending for a specific month within a category.
/// </summary>
public class MonthlySpendingDto
{
    /// <summary>
    /// Year.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month (1-12).
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Amount spent in this month.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Number of transactions in this month.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Month display name.
    /// </summary>
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}

/// <summary>
/// Summary of category spending across all categories.
/// </summary>
public class CategorySpendingSummaryDto
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
    /// Total spending across all categories.
    /// </summary>
    public decimal TotalSpending { get; set; }

    /// <summary>
    /// Total number of transactions.
    /// </summary>
    public int TotalTransactions { get; set; }

    /// <summary>
    /// Number of unique categories with spending.
    /// </summary>
    public int CategoryCount { get; set; }

    /// <summary>
    /// Spending data for each category.
    /// </summary>
    public List<CategorySpendingDto> Categories { get; set; } = [];

    /// <summary>
    /// Top spending categories (limited to top N).
    /// </summary>
    public List<CategorySpendingDto> TopCategories { get; set; } = [];
}

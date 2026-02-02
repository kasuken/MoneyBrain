namespace MoneyBrain.Web.Application.Reporting.Cashflow;

/// <summary>
/// Represents cashflow summary for a specific month.
/// </summary>
public class MonthlyCashflowDto
{
    /// <summary>
    /// Year of the cashflow period.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month of the cashflow period (1-12).
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Total income for the month.
    /// </summary>
    public decimal TotalIncome { get; set; }

    /// <summary>
    /// Total expenses for the month.
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Net cashflow (income - expenses).
    /// </summary>
    public decimal NetCashflow => TotalIncome - TotalExpenses;

    /// <summary>
    /// Total pending income (not yet posted).
    /// </summary>
    public decimal PendingIncome { get; set; }

    /// <summary>
    /// Total pending expenses (not yet posted).
    /// </summary>
    public decimal PendingExpenses { get; set; }

    /// <summary>
    /// Net pending cashflow (PendingIncome - PendingExpenses).
    /// </summary>
    public decimal PendingNetCashflow => PendingIncome - PendingExpenses;

    /// <summary>
    /// Income broken down by category.
    /// </summary>
    public List<CategoryCashflowDto> IncomeByCategory { get; set; } = [];

    /// <summary>
    /// Expenses broken down by category.
    /// </summary>
    public List<CategoryCashflowDto> ExpensesByCategory { get; set; } = [];

    /// <summary>
    /// Number of transactions in this period.
    /// </summary>
    public int TransactionCount { get; set; }
}

/// <summary>
/// Represents cashflow for a specific category within a period.
/// </summary>
public class CategoryCashflowDto
{
    /// <summary>
    /// Category ID (null for uncategorized).
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Category name.
    /// </summary>
    public string CategoryName { get; set; } = "Uncategorized";

    /// <summary>
    /// Category group name.
    /// </summary>
    public string? CategoryGroupName { get; set; }

    /// <summary>
    /// Total amount for this category in the period.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Percentage of total income or expenses.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Number of transactions in this category.
    /// </summary>
    public int TransactionCount { get; set; }
}

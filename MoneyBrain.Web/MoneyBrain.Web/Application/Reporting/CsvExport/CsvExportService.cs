using System.Text;
using MoneyBrain.Web.Application.Reporting.Cashflow;
using MoneyBrain.Web.Application.Reporting.CategorySpending;
using MoneyBrain.Web.Application.Reporting.BudgetComparison;
using MoneyBrain.Web.Application.Reporting.NetWorth;
using MoneyBrain.Web.Application.Reporting.AccountBalanceHistory;

namespace MoneyBrain.Web.Application.Reporting.CsvExport;

/// <summary>
/// Service for exporting reports to CSV format.
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Export cashflow data to CSV format.
    /// </summary>
    byte[] ExportCashflowToCsv(List<MonthlyCashflowDto> monthlyData);

    /// <summary>
    /// Export category spending data to CSV format.
    /// </summary>
    byte[] ExportCategorySpendingToCsv(CategorySpendingSummaryDto summary);

    /// <summary>
    /// Export budget comparison data to CSV format.
    /// </summary>
    byte[] ExportBudgetComparisonToCsv(BudgetComparisonSummaryDto summary);

    /// <summary>
    /// Export net worth history to CSV format.
    /// </summary>
    byte[] ExportNetWorthToCsv(NetWorthHistoryDto history);

    /// <summary>
    /// Export account balance history to CSV format.
    /// </summary>
    byte[] ExportAccountBalanceHistoryToCsv(AccountBalanceHistoryDto history);

    /// <summary>
    /// Export multi-account balance history to CSV format.
    /// </summary>
    byte[] ExportMultiAccountBalanceHistoryToCsv(MultiAccountBalanceHistoryDto history);
}

/// <summary>
/// Implementation of CSV export service.
/// </summary>
public class CsvExportService : ICsvExportService
{
    public byte[] ExportCashflowToCsv(List<MonthlyCashflowDto> monthlyData)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("Month,Total Income,Total Expenses,Net Cashflow");
        
        // Data rows
        foreach (var month in monthlyData.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            var monthName = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy");
            csv.AppendLine($"\"{monthName}\",{month.TotalIncome},{month.TotalExpenses},{month.NetCashflow}");
        }

        // Add category breakdown section
        csv.AppendLine();
        csv.AppendLine("Category Breakdown by Month");
        csv.AppendLine("Month,Category,Income,Expense");

        foreach (var month in monthlyData.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            var monthName = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy");
            foreach (var category in month.IncomeByCategory.OrderByDescending(c => c.Amount))
            {
                csv.AppendLine($"\"{monthName}\",\"{category.CategoryName}\",{category.Amount},0");
            }
            foreach (var category in month.ExpensesByCategory.OrderByDescending(c => c.Amount))
            {
                csv.AppendLine($"\"{monthName}\",\"{category.CategoryName}\",0,{category.Amount}");
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] ExportCategorySpendingToCsv(CategorySpendingSummaryDto summary)
    {
        var csv = new StringBuilder();
        
        // Summary section
        csv.AppendLine("Category Spending Summary");
        csv.AppendLine($"Total Spending,{summary.TotalSpending}");
        csv.AppendLine($"Category Count,{summary.CategoryCount}");
        csv.AppendLine($"Transaction Count,{summary.TotalTransactions}");
        csv.AppendLine();

        // Category details
        csv.AppendLine("Category,Total Spending,Percentage,Transaction Count,Avg Transaction");
        
        foreach (var category in summary.Categories.OrderByDescending(c => c.TotalSpending))
        {
            csv.AppendLine($"\"{category.CategoryName}\",{category.TotalSpending},{category.PercentageOfTotal:F2},{category.TransactionCount},{category.AverageTransactionAmount:F2}");
        }

        // Monthly breakdown by category
        csv.AppendLine();
        csv.AppendLine("Monthly Breakdown");
        csv.AppendLine("Category,Month,Amount,Transaction Count");

        foreach (var category in summary.Categories)
        {
            foreach (var month in category.MonthlyBreakdown.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
            {
                csv.AppendLine($"\"{category.CategoryName}\",\"{month.MonthName}\",{month.Amount},{month.TransactionCount}");
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] ExportBudgetComparisonToCsv(BudgetComparisonSummaryDto summary)
    {
        var csv = new StringBuilder();
        
        // Summary section
        csv.AppendLine("Budget vs Actual Comparison");
        csv.AppendLine($"Period,{summary.StartDate:yyyy-MM-dd} to {summary.EndDate:yyyy-MM-dd}");
        csv.AppendLine($"Total Budgeted,{summary.TotalBudgeted}");
        csv.AppendLine($"Total Actual,{summary.TotalActual}");
        csv.AppendLine($"Total Remaining,{summary.TotalRemaining}");
        csv.AppendLine($"Overall % Used,{summary.OverallPercentageUsed:F2}");
        csv.AppendLine();

        // Monthly summary
        csv.AppendLine("Monthly Summary");
        csv.AppendLine("Month,Budgeted,Actual,Remaining,% Used,Over Budget");
        
        foreach (var month in summary.MonthlyComparisons.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            csv.AppendLine($"\"{month.MonthName}\",{month.TotalBudgeted},{month.TotalActual},{month.Remaining},{month.PercentageUsed:F2},{month.IsOverBudget}");
        }

        // Category details by month
        csv.AppendLine();
        csv.AppendLine("Category Details");
        csv.AppendLine("Month,Category,Category Group,Budgeted,Actual,Remaining,% Used,Over Budget,Transactions");

        foreach (var month in summary.MonthlyComparisons.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            foreach (var category in month.Categories.OrderByDescending(c => c.Budgeted))
            {
                csv.AppendLine($"\"{month.MonthName}\",\"{category.CategoryName}\",\"{category.CategoryGroupName}\",{category.Budgeted},{category.Actual},{category.Remaining},{category.PercentageUsed:F2},{category.IsOverBudget},{category.TransactionCount}");
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] ExportNetWorthToCsv(NetWorthHistoryDto history)
    {
        var csv = new StringBuilder();
        
        // Summary section
        csv.AppendLine("Net Worth History");
        csv.AppendLine($"Period,{history.StartDate:yyyy-MM-dd} to {history.EndDate:yyyy-MM-dd}");
        csv.AppendLine($"Current Net Worth,{history.CurrentNetWorth}");
        csv.AppendLine($"Current Total Assets,{history.CurrentTotalAssets}");
        csv.AppendLine($"Current Total Liabilities,{history.CurrentTotalLiabilities}");
        csv.AppendLine($"Starting Net Worth,{history.StartingNetWorth}");
        csv.AppendLine($"Total Change,{history.TotalChange}");
        csv.AppendLine($"% Change,{history.PercentageChange:F2}");
        csv.AppendLine($"Peak Net Worth,{history.PeakNetWorth}");
        csv.AppendLine($"Lowest Net Worth,{history.LowestNetWorth}");
        csv.AppendLine();

        // Snapshot timeline
        csv.AppendLine("Net Worth Timeline");
        csv.AppendLine("Date,Assets,Liabilities,Net Worth,Change from Previous,% Change");
        
        foreach (var snapshot in history.Snapshots.OrderBy(s => s.Date))
        {
            var change = snapshot.ChangeFromPrevious?.ToString() ?? "";
            var pctChange = snapshot.PercentageChange?.ToString("F2") ?? "";
            csv.AppendLine($"{snapshot.Date:yyyy-MM-dd},{snapshot.TotalAssets},{snapshot.TotalLiabilities},{snapshot.NetWorth},{change},{pctChange}");
        }

        // Account balances by snapshot
        csv.AppendLine();
        csv.AppendLine("Account Balances by Date");
        csv.AppendLine("Date,Account Name,Account Type,Balance,Active");

        foreach (var snapshot in history.Snapshots.OrderBy(s => s.Date))
        {
            foreach (var account in snapshot.AccountBalances.OrderBy(a => a.AccountType).ThenBy(a => a.AccountName))
            {
                csv.AppendLine($"{snapshot.Date:yyyy-MM-dd},\"{account.AccountName}\",{account.AccountType},{account.Balance},{account.IsActive}");
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] ExportAccountBalanceHistoryToCsv(AccountBalanceHistoryDto history)
    {
        var csv = new StringBuilder();
        
        // Account summary
        csv.AppendLine("Account Balance History");
        csv.AppendLine($"Account,\"{history.AccountName}\"");
        csv.AppendLine($"Account Type,{history.AccountType}");
        csv.AppendLine($"Account SubType,{history.AccountSubType}");
        csv.AppendLine($"Current Balance,{history.CurrentBalance}");
        csv.AppendLine($"Opening Balance,{history.OpeningBalance}");
        csv.AppendLine($"Balance Change,{history.BalanceChange}");
        csv.AppendLine($"% Change,{history.PercentageChange:F2}");
        csv.AppendLine($"Peak Balance,{history.PeakBalance}");
        csv.AppendLine($"Lowest Balance,{history.LowestBalance}");
        csv.AppendLine($"Average Balance,{history.AverageBalance}");
        csv.AppendLine();

        // Balance timeline
        csv.AppendLine("Balance Timeline");
        csv.AppendLine("Date,Balance,Change from Previous,% Change from Previous");
        
        foreach (var snapshot in history.Snapshots.OrderBy(s => s.Date))
        {
            var change = snapshot.ChangeFromPrevious?.ToString() ?? "";
            var pctChange = snapshot.PercentageChangeFromPrevious?.ToString("F2") ?? "";
            csv.AppendLine($"{snapshot.Date:yyyy-MM-dd},{snapshot.Balance},{change},{pctChange}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] ExportMultiAccountBalanceHistoryToCsv(MultiAccountBalanceHistoryDto history)
    {
        var csv = new StringBuilder();
        
        // Summary section
        csv.AppendLine("Multi-Account Balance History");
        csv.AppendLine($"Period,{history.StartDate:yyyy-MM-dd} to {history.EndDate:yyyy-MM-dd}");
        csv.AppendLine($"Account Count,{history.AccountCount}");
        csv.AppendLine($"Total Current Balance,{history.TotalCurrentBalance}");
        csv.AppendLine($"Total Opening Balance,{history.TotalOpeningBalance}");
        csv.AppendLine($"Total Change,{history.TotalChange}");
        csv.AppendLine();

        // Account summaries
        csv.AppendLine("Account Summaries");
        csv.AppendLine("Account Name,Type,SubType,Current Balance,Opening Balance,Change,% Change,Peak,Lowest,Average");
        
        foreach (var account in history.Accounts.OrderBy(a => a.AccountType).ThenBy(a => a.AccountName))
        {
            csv.AppendLine($"\"{account.AccountName}\",{account.AccountType},{account.AccountSubType},{account.CurrentBalance},{account.OpeningBalance},{account.BalanceChange},{account.PercentageChange:F2},{account.PeakBalance},{account.LowestBalance},{account.AverageBalance}");
        }

        // Balance timeline by account
        csv.AppendLine();
        csv.AppendLine("Balance Timeline");
        csv.AppendLine("Date,Account Name,Balance");

        // Get all unique dates
        var allDates = history.Accounts
            .SelectMany(a => a.Snapshots.Select(s => s.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var date in allDates)
        {
            foreach (var account in history.Accounts.OrderBy(a => a.AccountName))
            {
                var snapshot = account.Snapshots.FirstOrDefault(s => s.Date == date);
                if (snapshot != null)
                {
                    csv.AppendLine($"{date:yyyy-MM-dd},\"{account.AccountName}\",{snapshot.Balance}");
                }
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }
}

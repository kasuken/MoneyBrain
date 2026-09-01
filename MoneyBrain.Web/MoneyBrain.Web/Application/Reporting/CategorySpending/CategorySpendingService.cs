using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Reporting.CategorySpending;

/// <summary>
/// Service for analyzing category spending using double-entry ledger data.
/// Focuses on expense transactions (debits to category accounts).
/// </summary>
public class CategorySpendingService(IDbContextFactory<ApplicationDbContext> contextFactory) : ICategorySpendingService
{
    public async Task<CategorySpendingSummaryDto> GetCategorySpendingSummaryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Get all expense ledger entries (debits to categories) for the period
        // Expenses are represented as debits in the ledger
        var expenseEntries = await context.LedgerEntries
            .Include(le => le.Category)
                .ThenInclude(c => c!.CategoryGroup)
            .Include(le => le.Transaction)
            .Where(le => le.UserId == userId &&
                         le.EntryDate >= startDate &&
                         le.EntryDate <= endDate &&
                         le.CategoryId != null &&
                         le.DebitAmount > 0 && // Expenses are debits
                         le.Transaction.Status == TransactionStatus.Posted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var summary = new CategorySpendingSummaryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalSpending = expenseEntries.Sum(e => e.DebitAmount),
            TotalTransactions = expenseEntries.Select(e => e.TransactionId).Distinct().Count()
        };

        // Group by category — expenseEntries is already in-memory so this is a local GroupBy
        var categoryGroups = expenseEntries
            .GroupBy(e => new
            {
                CategoryId = e.CategoryId!.Value,
                CategoryName = e.Category?.Name ?? "Unknown",
                CategoryGroupName = e.Category?.CategoryGroup?.Name
            });

        var categories = new List<CategorySpendingDto>();

        foreach (var group in categoryGroups)
        {
            // Sum and count directly on the group to avoid a redundant ToList() materialisation
            var totalCategorySpending = group.Sum(e => e.DebitAmount);
            var categoryTransactionCount = group.Select(e => e.TransactionId).Distinct().Count();

            // Group by month for this category
            var monthlyBreakdown = group
                .GroupBy(e => new { e.EntryDate.Year, e.EntryDate.Month })
                .Select(mg => new MonthlySpendingDto
                {
                    Year = mg.Key.Year,
                    Month = mg.Key.Month,
                    Amount = mg.Sum(e => e.DebitAmount),
                    TransactionCount = mg.Select(e => e.TransactionId).Distinct().Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            var categoryDto = new CategorySpendingDto
            {
                CategoryId = group.Key.CategoryId,
                CategoryName = group.Key.CategoryName,
                CategoryGroupName = group.Key.CategoryGroupName,
                TotalSpending = totalCategorySpending,
                TransactionCount = categoryTransactionCount,
                MonthlyBreakdown = monthlyBreakdown,
                PercentageOfTotal = summary.TotalSpending > 0
                    ? (totalCategorySpending / summary.TotalSpending * 100)
                    : 0
            };

            categories.Add(categoryDto);
        }

        // Sort by total spending descending
        summary.Categories = categories.OrderByDescending(c => c.TotalSpending).ToList();
        summary.CategoryCount = categories.Count;

        // Get top N categories
        summary.TopCategories = summary.Categories.Take(topCount).ToList();

        return summary;
    }

    public async Task<CategorySpendingDto?> GetCategorySpendingDetailsAsync(
        string userId,
        int categoryId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Get expense entries for this specific category
        var entries = await context.LedgerEntries
            .Include(le => le.Category)
                .ThenInclude(c => c!.CategoryGroup)
            .Include(le => le.Transaction)
            .Where(le => le.UserId == userId &&
                         le.CategoryId == categoryId &&
                         le.EntryDate >= startDate &&
                         le.EntryDate <= endDate &&
                         le.DebitAmount > 0 &&
                         le.Transaction.Status == TransactionStatus.Posted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return null;
        }

        var category = entries.First().Category;

        // Group by month
        var monthlyBreakdown = entries
            .GroupBy(e => new { e.EntryDate.Year, e.EntryDate.Month })
            .Select(mg => new MonthlySpendingDto
            {
                Year = mg.Key.Year,
                Month = mg.Key.Month,
                Amount = mg.Sum(e => e.DebitAmount),
                TransactionCount = mg.Select(e => e.TransactionId).Distinct().Count()
            })
            .OrderBy(m => m.Year)
            .ThenBy(m => m.Month)
            .ToList();

        // Calculate total spending for percentage (across all categories)
        var totalSpending = await context.LedgerEntries
            .Where(le => le.UserId == userId &&
                         le.EntryDate >= startDate &&
                         le.EntryDate <= endDate &&
                         le.CategoryId != null &&
                         le.DebitAmount > 0 &&
                         le.Transaction.Status == TransactionStatus.Posted)
            .SumAsync(le => le.DebitAmount, cancellationToken);

        var categorySpending = entries.Sum(e => e.DebitAmount);

        return new CategorySpendingDto
        {
            CategoryId = categoryId,
            CategoryName = category?.Name ?? "Unknown",
            CategoryGroupName = category?.CategoryGroup?.Name,
            TotalSpending = categorySpending,
            TransactionCount = entries.Select(e => e.TransactionId).Distinct().Count(),
            MonthlyBreakdown = monthlyBreakdown,
            PercentageOfTotal = totalSpending > 0 ? (categorySpending / totalSpending * 100) : 0
        };
    }
}

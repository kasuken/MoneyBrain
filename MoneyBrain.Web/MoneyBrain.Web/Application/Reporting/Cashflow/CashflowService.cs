using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Reporting.Cashflow;

/// <summary>
/// Service for generating cashflow reports using double-entry ledger data.
/// Analyzes income and expenses from ledger entries with category associations.
/// </summary>
public class CashflowService : ICashflowService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public CashflowService(ApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }
    public async Task<List<MonthlyCashflowDto>> GetMonthlyCashflowAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // Default to last 6 months if not specified
        // DateTime.UtcNow.Date is used as the "today" reference date so the default
        // range is stable regardless of server local timezone.
        var end = endDate ?? DateTime.UtcNow.Date;
        var start = startDate ?? end.AddMonths(-5).Date;

        // Get all ledger entries with categories for the period
        var entries = await _context.LedgerEntries
            .Include(le => le.Category)
                .ThenInclude(c => c!.CategoryGroup)
            .Include(le => le.Transaction)
            .Where(le => le.UserId == userId &&
                         le.EntryDate >= start &&
                         le.EntryDate <= end &&
                         le.CategoryId != null) // Only entries with categories (income/expense)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Group by year and month
        var monthlyGroups = entries
            .GroupBy(le => new { le.EntryDate.Year, le.EntryDate.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month);

        var result = new List<MonthlyCashflowDto>();

        foreach (var monthGroup in monthlyGroups)
        {
            var monthData = BuildMonthlyCashflow(
                monthGroup.Key.Year,
                monthGroup.Key.Month,
                monthGroup.ToList());

            result.Add(monthData);
        }

        // Fill in missing months with zero data
        var current = new DateTime(start.Year, start.Month, 1);
        var lastMonth = new DateTime(end.Year, end.Month, 1);
        var existingMonths = result.Select(r => (r.Year, r.Month)).ToHashSet();

        while (current <= lastMonth)
        {
            var monthKey = (current.Year, current.Month);
            if (!existingMonths.Contains(monthKey))
            {
                result.Add(new MonthlyCashflowDto
                {
                    Year = current.Year,
                    Month = current.Month,
                    TotalIncome = 0,
                    TotalExpenses = 0,
                    TransactionCount = 0
                });

                existingMonths.Add(monthKey);
            }

            current = current.AddMonths(1);
        }

        return result.OrderBy(r => r.Year).ThenBy(r => r.Month).ToList();
    }

    public async Task<MonthlyCashflowDto> GetMonthCashflowAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForMonthCashflow(userId, year, month);
        var cached = await _cacheService.GetAsync<MonthlyCashflowDto>(cacheKey);
        if (cached != null)
            return cached;

        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get all ledger entries with categories for the month
        var entries = await _context.LedgerEntries
            .Include(le => le.Category)
                .ThenInclude(c => c!.CategoryGroup)
            .Include(le => le.Transaction)
            .Where(le => le.UserId == userId &&
                         le.EntryDate >= startDate &&
                         le.EntryDate <= endDate &&
                         le.CategoryId != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = BuildMonthlyCashflow(year, month, entries);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));

        return result;
    }

    /// <summary>
    /// Build monthly cashflow data from ledger entries.
    /// Income = Credits to category accounts (positive)
    /// Expenses = Debits to category accounts (positive)
    /// </summary>
    private static MonthlyCashflowDto BuildMonthlyCashflow(
        int year,
        int month,
        List<Domain.Entities.LedgerEntry> entries)
    {
        var result = new MonthlyCashflowDto
        {
            Year = year,
            Month = month
        };

        // Split entries into posted and pending
        var postedEntries = entries.Where(le => le.Transaction.Status == TransactionStatus.Posted).ToList();
        var pendingEntries = entries.Where(le => le.Transaction.Status == TransactionStatus.Pending).ToList();

        // Separate posted income (credits to categories) and expenses (debits to categories)
        var incomeEntries = postedEntries.Where(le => le.CreditAmount > 0).ToList();
        var expenseEntries = postedEntries.Where(le => le.DebitAmount > 0).ToList();

        result.TotalIncome = incomeEntries.Sum(le => le.CreditAmount);
        result.TotalExpenses = expenseEntries.Sum(le => le.DebitAmount);

        // Calculate pending amounts
        result.PendingIncome = pendingEntries.Where(le => le.CreditAmount > 0).Sum(le => le.CreditAmount);
        result.PendingExpenses = pendingEntries.Where(le => le.DebitAmount > 0).Sum(le => le.DebitAmount);

        // Get unique transaction IDs to count transactions
        result.TransactionCount = entries
            .Select(le => le.TransactionId)
            .Distinct()
            .Count();

        // Group income by category
        var incomeByCategory = incomeEntries
            .GroupBy(le => new
            {
                CategoryId = le.CategoryId,
                CategoryName = le.Category?.Name ?? "Uncategorized",
                CategoryGroupName = le.Category?.CategoryGroup?.Name
            })
            .Select(g => new CategoryCashflowDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                CategoryGroupName = g.Key.CategoryGroupName,
                Amount = g.Sum(le => le.CreditAmount),
                Percentage = result.TotalIncome > 0 ? (g.Sum(le => le.CreditAmount) / result.TotalIncome * 100) : 0,
                TransactionCount = g.Select(le => le.TransactionId).Distinct().Count()
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Group expenses by category
        var expensesByCategory = expenseEntries
            .GroupBy(le => new
            {
                CategoryId = le.CategoryId,
                CategoryName = le.Category?.Name ?? "Uncategorized",
                CategoryGroupName = le.Category?.CategoryGroup?.Name
            })
            .Select(g => new CategoryCashflowDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                CategoryGroupName = g.Key.CategoryGroupName,
                Amount = g.Sum(le => le.DebitAmount),
                Percentage = result.TotalExpenses > 0 ? (g.Sum(le => le.DebitAmount) / result.TotalExpenses * 100) : 0,
                TransactionCount = g.Select(le => le.TransactionId).Distinct().Count()
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        result.IncomeByCategory = incomeByCategory;
        result.ExpensesByCategory = expensesByCategory;

        return result;
    }
}

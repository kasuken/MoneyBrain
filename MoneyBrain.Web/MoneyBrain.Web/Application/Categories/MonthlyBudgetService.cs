using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Categories;

/// <summary>
/// Manages per-category monthly budgets (defaults and month-specific overrides).
/// </summary>
public class MonthlyBudgetService : IMonthlyBudgetService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MonthlyBudgetService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <inheritdoc />
    public async Task<MonthlyBudget?> GetEffectiveBudgetAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Check month-specific override first
        var monthOverride = await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId &&
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (monthOverride != null)
            return monthOverride;

        // Fall back to default budget
        return await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyBudget?> GetDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyBudget?> GetMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId &&
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<MonthlyBudget>> GetEffectiveBudgetsForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Get all month-specific overrides
        var overrides = await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && !mb.IsDefault && mb.Year == year && mb.Month == month)
            .ToListAsync(cancellationToken);

        var overrideCategoryIds = overrides.Select(o => o.CategoryId).ToHashSet();

        // Get default budgets for categories without overrides
        var defaults = await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && mb.IsDefault && !overrideCategoryIds.Contains(mb.CategoryId))
            .ToListAsync(cancellationToken);

        // Combine and sort
        return overrides.Concat(defaults)
            .OrderBy(mb => mb.Category.CategoryGroup.SortOrder)
            .ThenBy(mb => mb.Category.CategoryGroup.Name)
            .ThenBy(mb => mb.Category.SortOrder)
            .ThenBy(mb => mb.Category.Name)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<MonthlyBudget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && mb.IsDefault)
            .OrderBy(mb => mb.Category.CategoryGroup.SortOrder)
            .ThenBy(mb => mb.Category.CategoryGroup.Name)
            .ThenBy(mb => mb.Category.SortOrder)
            .ThenBy(mb => mb.Category.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<MonthlyBudget>> GetMonthOverridesAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && !mb.IsDefault && mb.Year == year && mb.Month == month)
            .OrderBy(mb => mb.Category.CategoryGroup.SortOrder)
            .ThenBy(mb => mb.Category.CategoryGroup.Name)
            .ThenBy(mb => mb.Category.SortOrder)
            .ThenBy(mb => mb.Category.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MonthlyBudget> SetDefaultBudgetAsync(int categoryId, string userId, decimal plannedAmount, bool allowRollover = false, string? notes = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify the category belongs to the user
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        var existingBudget = await context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);

        if (existingBudget != null)
            return await UpdateExistingBudgetAsync(context, existingBudget, plannedAmount, allowRollover, notes, cancellationToken);

        var newBudget = new MonthlyBudget
        {
            UserId = userId,
            CategoryId = categoryId,
            IsDefault = true,
            Year = null,
            Month = null,
            PlannedAmount = plannedAmount,
            AllowRollover = allowRollover,
            Notes = notes
        };

        context.MonthlyBudgets.Add(newBudget);
        await context.SaveChangesAsync(cancellationToken);
        return newBudget;
    }

    /// <inheritdoc />
    public async Task<MonthlyBudget> SetMonthOverrideAsync(int categoryId, string userId, int year, int month, decimal plannedAmount, bool allowRollover = false, string? notes = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify the category belongs to the user
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        var existingBudget = await context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId &&
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (existingBudget != null)
            return await UpdateExistingBudgetAsync(context, existingBudget, plannedAmount, allowRollover, notes, cancellationToken);

        var newBudget = new MonthlyBudget
        {
            UserId = userId,
            CategoryId = categoryId,
            IsDefault = false,
            Year = year,
            Month = month,
            PlannedAmount = plannedAmount,
            AllowRollover = allowRollover,
            Notes = notes
        };

        context.MonthlyBudgets.Add(newBudget);
        await context.SaveChangesAsync(cancellationToken);
        return newBudget;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var budget = await context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);

        if (budget == null)
            return false;

        context.MonthlyBudgets.Remove(budget);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var budget = await context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId &&
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (budget == null)
            return false;

        context.MonthlyBudgets.Remove(budget);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<decimal> GetCategoryActualSpendingAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transactionExpenses = await context.Transactions
            .Where(t => t.CategoryId == categoryId &&
                       t.UserId == userId &&
                       t.Status == TransactionStatus.Posted &&
                       t.Date >= startDate && t.Date <= endDate &&
                       t.Amount < 0)
            .SumAsync(t => -t.Amount, cancellationToken);

        var splitExpenses = await context.TransactionSplits
            .Include(ts => ts.Transaction)
            .Where(ts => ts.CategoryId == categoryId &&
                        ts.Transaction.UserId == userId &&
                        ts.Transaction.Status == TransactionStatus.Posted &&
                        ts.Transaction.Date >= startDate && ts.Transaction.Date <= endDate &&
                        ts.Amount < 0)
            .SumAsync(ts => -ts.Amount, cancellationToken);

        return transactionExpenses + splitExpenses;
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, decimal>> GetAllCategoriesActualSpendingAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var result = new Dictionary<int, decimal>();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transactionExpenses = await context.Transactions
            .Where(t => t.CategoryId != null &&
                       t.UserId == userId &&
                       t.Status == TransactionStatus.Posted &&
                       t.Date >= startDate && t.Date <= endDate &&
                       t.Amount < 0)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(t => -t.Amount) })
            .ToListAsync(cancellationToken);

        foreach (var item in transactionExpenses)
            result[item.CategoryId] = item.Amount;

        var splitExpenses = await context.TransactionSplits
            .Include(ts => ts.Transaction)
            .Where(ts => ts.CategoryId != null &&
                        ts.Transaction.UserId == userId &&
                        ts.Transaction.Status == TransactionStatus.Posted &&
                        ts.Transaction.Date >= startDate && ts.Transaction.Date <= endDate &&
                        ts.Amount < 0)
            .GroupBy(ts => ts.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(ts => -ts.Amount) })
            .ToListAsync(cancellationToken);

        foreach (var item in splitExpenses)
        {
            if (result.ContainsKey(item.CategoryId))
                result[item.CategoryId] += item.Amount;
            else
                result[item.CategoryId] = item.Amount;
        }

        return result;
    }

    /// <summary>
    /// Updates a <see cref="MonthlyBudget"/> with new values and saves. Shared by
    /// <see cref="SetDefaultBudgetAsync"/> and <see cref="SetMonthOverrideAsync"/>.
    /// </summary>
    private static async Task<MonthlyBudget> UpdateExistingBudgetAsync(
        ApplicationDbContext context,
        MonthlyBudget budget,
        decimal plannedAmount,
        bool allowRollover,
        string? notes,
        CancellationToken cancellationToken)
    {
        budget.PlannedAmount = plannedAmount;
        budget.AllowRollover = allowRollover;
        budget.Notes = notes;
        budget.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return budget;
    }
}

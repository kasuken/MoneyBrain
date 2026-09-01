using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Categories;

public class CategoryService : ICategoryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ICacheService _cacheService;

    public CategoryService(IDbContextFactory<ApplicationDbContext> contextFactory, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _cacheService = cacheService;
    }

    public async Task<List<CategoryGroup>> GetCategoryGroupsAsync(string userId, bool includeCategories = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var cacheKey = CacheKeyHelper.ForUserCategories(userId);
        var cached = await _cacheService.GetAsync<List<CategoryGroup>>(cacheKey);
        if (cached != null)
            return cached;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<CategoryGroup> query = context.CategoryGroups
            .ForUser(userId)
            .Where(cg => cg.IsActive)
            .OrderBy(cg => cg.SortOrder)
           .ThenBy(cg => cg.Name);

        if (includeCategories)
        {
            query = query.Include(cg => cg.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name));
        }

        var result = await query.AsNoTracking().ToListAsync(cancellationToken);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
        return result;
    }

    public async Task<List<Category>> GetCategoriesAsync(string userId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Categories
            .Include(c => c.CategoryGroup)
            .ForUser(userId);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.CategoryGroup.SortOrder)
            .ThenBy(c => c.CategoryGroup.Name)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetCategoryByIdAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
    }

    public async Task<CategoryGroup> CreateCategoryGroupAsync(string userId, string name, CategoryType type = CategoryType.Expense, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var maxSortOrder = await context.CategoryGroups
            .Where(cg => cg.UserId == userId)
            .MaxAsync(cg => (int?)cg.SortOrder, cancellationToken) ?? 0;

        var categoryGroup = new CategoryGroup
        {
            UserId = userId,
            Name = name,
            Type = type,
            SortOrder = maxSortOrder + 1
        };

        context.CategoryGroups.Add(categoryGroup);
        await context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));

        return categoryGroup;
    }

    public async Task<CategoryGroup?> GetCategoryGroupByIdAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CategoryGroups
            .Include(cg => cg.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);
    }

    public async Task<bool> UpdateCategoryGroupAsync(int categoryGroupId, string userId, string name, CategoryType type, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var group = await context.CategoryGroups
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);

        if (group == null)
            return false;

        group.Name = name;
        group.Type = type;
        group.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));
        return true;
    }

    public async Task<bool> DeleteCategoryGroupAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var group = await context.CategoryGroups
            .Include(cg => cg.Categories)
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);

        if (group == null)
            return false;

        // Check if group has active categories
        if (group.Categories.Any(c => c.IsActive))
            throw new InvalidOperationException("Cannot delete category group with active categories. Move or delete categories first.");

        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));
        return true;
    }

    public async Task<bool> ReorderCategoryGroupsAsync(string userId, List<int> orderedGroupIds, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var groups = await context.CategoryGroups
            .Where(cg => cg.UserId == userId && orderedGroupIds.Contains(cg.Id))
            .ToListAsync(cancellationToken);

        if (groups.Count != orderedGroupIds.Count)
            return false;

        var groupsById = groups.ToDictionary(g => g.Id);

        for (int i = 0; i < orderedGroupIds.Count; i++)
        {
            var group = groupsById[orderedGroupIds[i]];
            group.SortOrder = i + 1;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderCategoriesAsync(string userId, int categoryGroupId, List<int> orderedCategoryIds, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var categories = await context.Categories
            .Where(c => c.UserId == userId && c.CategoryGroupId == categoryGroupId && orderedCategoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (categories.Count != orderedCategoryIds.Count)
            return false;

        var categoriesById = categories.ToDictionary(c => c.Id);

        for (int i = 0; i < orderedCategoryIds.Count; i++)
        {
            var category = categoriesById[orderedCategoryIds[i]];
            category.SortOrder = i + 1;
            category.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Category> CreateCategoryAsync(string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var maxSortOrder = await context.Categories
            .Where(c => c.UserId == userId && c.CategoryGroupId == categoryGroupId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        var category = new Category
        {
            UserId = userId,
            Name = name,
            CategoryGroupId = categoryGroupId,
            SortOrder = maxSortOrder + 1
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));

        return category;
    }

    public async Task<bool> UpdateCategoryAsync(int categoryId, string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.Name = name;
        category.CategoryGroupId = categoryGroupId;
        category.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));
        return true;
    }

    public async Task SeedDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Check if user already has categories
        var hasCategories = await context.Categories.AnyAsync(c => c.UserId == userId, cancellationToken);
        if (hasCategories)
            return;

        // Create default category groups and categories
        var defaults = new Dictionary<string, (CategoryType Type, string[] Categories)>
        {
            { "Income", (CategoryType.Income, new[] {  "Salary", "Freelance", "Investments", "Other Income" }) },
            { "Housing", (CategoryType.Expense, new[] { "Rent/Mortgage", "Utilities", "Home Maintenance", "Home Insurance" }) },
            { "Transportation", (CategoryType.Expense, new[] { "Gas/Fuel", "Public Transit", "Parking", "Car Payment", "Auto Maintenance" }) },
            { "Food", (CategoryType.Expense, new[] { "Groceries", "Restaurants", "Coffee/Snacks" }) },
            { "Shopping", (CategoryType.Expense, new[] { "Clothing", "Electronics", "Home Goods", "Other Shopping" }) },
            { "Entertainment", (CategoryType.Expense, new[] { "Subscriptions", "Movies/Events", "Hobbies" }) },
            { "Health", (CategoryType.Expense, new[] { "Medical", "Pharmacy", "Fitness", "Health Insurance" }) },
            { "Personal", (CategoryType.Expense, new[] { "Personal Care", "Education", "Gifts" }) },
            { "Miscellaneous", (CategoryType.Expense, new[] { "Fees/Charges", "Taxes", "Uncategorized" }) }
        };

        int groupOrder = 1;
        foreach (var (groupName, (type, categories)) in defaults)
        {
            var group = new CategoryGroup
            {
                UserId = userId,
                Name = groupName,
                Type = type,
                SortOrder = groupOrder++
            };
            context.CategoryGroups.Add(group);
            await context.SaveChangesAsync(cancellationToken);

            int categoryOrder = 1;
            foreach (var categoryName in categories)
            {
                var category = new Category
                {
                    UserId = userId,
                    Name = categoryName,
                    CategoryGroupId = group.Id,
                    SortOrder = categoryOrder++
                };
                context.Categories.Add(category);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> RenameCategoryAsync(int categoryId, string userId, string newName, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        // Check if new name already exists in the same group
        var nameExists = await context.Categories
            .AnyAsync(c => c.UserId == userId && c.CategoryGroupId == category.CategoryGroupId && 
                          c.Name == newName && c.Id != categoryId && c.IsActive, cancellationToken);

        if (nameExists)
            throw new InvalidOperationException($"A category named '{newName}' already exists in this group");

        category.Name = newName;
        category.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MergeCategoriesAsync(int sourceCategoryId, int targetCategoryId, string userId, CancellationToken cancellationToken = default)
    {
        if (sourceCategoryId == targetCategoryId)
            throw new InvalidOperationException("Cannot merge a category into itself");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify both categories exist and belong to user
        var sourceCategory = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == sourceCategoryId && c.UserId == userId, cancellationToken);

        var targetCategory = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == targetCategoryId && c.UserId == userId, cancellationToken);

        if (sourceCategory == null || targetCategory == null)
            throw new InvalidOperationException("Source or target category not found");

        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Update all transactions from source to target
            var transactions = await context.Transactions
                .Where(t => t.CategoryId == sourceCategoryId && t.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var txn in transactions)
            {
                txn.CategoryId = targetCategoryId;
                txn.UpdatedAt = DateTime.UtcNow;
            }

            // Update all transaction splits from source to target
            var splits = await context.TransactionSplits
                .Include(ts => ts.Transaction)
                .Where(ts => ts.CategoryId == sourceCategoryId && ts.Transaction.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var split in splits)
            {
                split.CategoryId = targetCategoryId;
            }

            // Re-point ledger entries so category-based reports (e.g. budget comparison)
            // reflect the merge for historical data as well.
            await context.LedgerEntries
                .Where(le => le.CategoryId == sourceCategoryId && le.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(le => le.CategoryId, targetCategoryId), cancellationToken);

            // Handle monthly budgets - merge or delete
            var sourceBudgets = await context.MonthlyBudgets
                .Where(mb => mb.CategoryId == sourceCategoryId && mb.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var sourceBudget in sourceBudgets)
            {
                // Check if target already has a budget for the same period
                var targetBudget = await context.MonthlyBudgets
                    .FirstOrDefaultAsync(mb => mb.CategoryId == targetCategoryId && mb.UserId == userId &&
                                              mb.IsDefault == sourceBudget.IsDefault &&
                                              mb.Year == sourceBudget.Year && mb.Month == sourceBudget.Month, 
                                        cancellationToken);

                if (targetBudget == null)
                {
                    // Move the source budget to target
                    sourceBudget.CategoryId = targetCategoryId;
                    sourceBudget.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Target already has a budget, just delete source
                    context.MonthlyBudgets.Remove(sourceBudget);
                }
            }

            // Handle named budget categories
            var sourceBudgetCategories = await context.BudgetCategories
                .Where(bc => bc.CategoryId == sourceCategoryId)
                .Include(bc => bc.Budget)
                .Where(bc => bc.Budget.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var sourceBudgetCategory in sourceBudgetCategories)
            {
                // Check if target already has this budget assignment
                var targetBudgetCategory = await context.BudgetCategories
                    .FirstOrDefaultAsync(bc => bc.BudgetId == sourceBudgetCategory.BudgetId && 
                                              bc.CategoryId == targetCategoryId, cancellationToken);

                if (targetBudgetCategory == null)
                {
                    // Move the source budget category to target
                    sourceBudgetCategory.CategoryId = targetCategoryId;
                }
                else
                {
                    // Target already has this budget, just delete source
                    context.BudgetCategories.Remove(sourceBudgetCategory);
                }
            }

            // Soft-delete the source category
            sourceCategory.IsActive = false;
            sourceCategory.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Invalidate cached category data so post-merge reads are fresh.
            await _cacheService.RemoveAsync(CacheKeyHelper.ForUserCategories(userId));

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CategoryUsageStats> GetCategoryUsageStatsAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify category belongs to user
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        // Get transaction stats
        var transactions = await context.Transactions
            .Where(t => t.CategoryId == categoryId && t.UserId == userId && t.Status == TransactionStatus.Posted)
            .ToListAsync(cancellationToken);

        // Get split stats
        var splits = await context.TransactionSplits
            .Include(ts => ts.Transaction)
            .Where(ts => ts.CategoryId == categoryId && ts.Transaction.UserId == userId && ts.Transaction.Status == TransactionStatus.Posted)
            .ToListAsync(cancellationToken);

        var stats = new CategoryUsageStats
        {
            TransactionCount = transactions.Count,
            SplitCount = splits.Count,
            TotalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount) + splits.Where(s => s.Amount > 0).Sum(s => s.Amount),
            TotalExpense = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount)) + Math.Abs(splits.Where(s => s.Amount < 0).Sum(s => s.Amount)),
            FirstUsed = transactions.Any() || splits.Any() 
                ? new[] { transactions.Any() ? transactions.Min(t => t.Date) : DateTime.MaxValue, splits.Any() ? splits.Min(s => s.Transaction.Date) : DateTime.MaxValue }.Min()
                : null,
            LastUsed = transactions.Any() || splits.Any()
                ? new[] { transactions.Any() ? transactions.Max(t => t.Date) : DateTime.MinValue, splits.Any() ? splits.Max(s => s.Transaction.Date) : DateTime.MinValue }.Max()
                : null
        };

        stats.NetAmount = stats.TotalIncome - stats.TotalExpense;

        // Calculate months with activity
        var allDates = transactions.Select(t => new { t.Date.Year, t.Date.Month })
            .Concat(splits.Select(s => new { s.Transaction.Date.Year, s.Transaction.Date.Month }))
            .Distinct()
            .ToList();

        stats.MonthsWithActivity = allDates.Count;

        return stats;
    }

    public async Task<List<Transaction>> GetTransactionsByCategoryAsync(int categoryId, string userId, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify category belongs to user
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        return await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .Where(t => t.CategoryId == categoryId && t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MonthlySpending>> GetMonthlySpendingAsync(int categoryId, string userId, int monthsBack = 12, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify category belongs to user
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        var startDate = DateTime.UtcNow.AddMonths(-monthsBack);

        // Get transactions
        var transactions = await context.Transactions
            .Where(t => t.CategoryId == categoryId && t.UserId == userId && 
                       t.Status == TransactionStatus.Posted &&
                       t.Date >= startDate)
            .ToListAsync(cancellationToken);

        // Get splits
        var splits = await context.TransactionSplits
            .Include(ts => ts.Transaction)
            .Where(ts => ts.CategoryId == categoryId && 
                        ts.Transaction.UserId == userId &&
                        ts.Transaction.Status == TransactionStatus.Posted &&
                        ts.Transaction.Date >= startDate)
            .ToListAsync(cancellationToken);

        // Group by month
        var monthlyData = transactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new MonthlySpending
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                Expense = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                TransactionCount = g.Count()
            })
            .ToList();

        // Add split data
        var splitMonthlyData = splits
            .GroupBy(s => new { s.Transaction.Date.Year, s.Transaction.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Income = g.Where(s => s.Amount > 0).Sum(s => s.Amount),
                Expense = Math.Abs(g.Where(s => s.Amount < 0).Sum(s => s.Amount)),
                Count = g.Count()
            });

        foreach (var splitGroup in splitMonthlyData)
        {
            var existing = monthlyData.FirstOrDefault(m => m.Year == splitGroup.Year && m.Month == splitGroup.Month);
            if (existing != null)
            {
                existing.Income += splitGroup.Income;
                existing.Expense += splitGroup.Expense;
                existing.TransactionCount += splitGroup.Count;
            }
            else
            {
                monthlyData.Add(new MonthlySpending
                {
                    Year = splitGroup.Year,
                    Month = splitGroup.Month,
                    Income = splitGroup.Income,
                    Expense = splitGroup.Expense,
                    TransactionCount = splitGroup.Count
                });
            }
        }

        // Calculate net and sort
        foreach (var month in monthlyData)
        {
            month.Net = month.Income - month.Expense;
        }

        return monthlyData
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ToList();
    }

}

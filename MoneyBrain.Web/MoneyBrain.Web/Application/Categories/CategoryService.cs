using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Categories;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;

    public CategoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryGroup>> GetCategoryGroupsAsync(string userId, bool includeCategories = false, CancellationToken cancellationToken = default)
    {
        IQueryable<CategoryGroup> query = _context.CategoryGroups
            .Where(cg => cg.UserId == userId && cg.IsActive)
            .OrderBy(cg => cg.SortOrder)
           .ThenBy(cg => cg.Name);

        if (includeCategories)
        {
            query = query.Include(cg => cg.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<Category>> GetCategoriesAsync(string userId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Categories
            .Include(c => c.CategoryGroup)
            .Where(c => c.UserId == userId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.CategoryGroup.SortOrder)
            .ThenBy(c => c.CategoryGroup.Name)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetCategoryByIdAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
    }

    public async Task<CategoryGroup> CreateCategoryGroupAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await _context.CategoryGroups
            .Where(cg => cg.UserId == userId)
            .MaxAsync(cg => (int?)cg.SortOrder, cancellationToken) ?? 0;

        var categoryGroup = new CategoryGroup
        {
            UserId = userId,
            Name = name,
            SortOrder = maxSortOrder + 1
        };

        _context.CategoryGroups.Add(categoryGroup);
        await _context.SaveChangesAsync(cancellationToken);

        return categoryGroup;
    }

    public async Task<CategoryGroup?> GetCategoryGroupByIdAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.CategoryGroups
            .Include(cg => cg.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);
    }

    public async Task<bool> UpdateCategoryGroupAsync(int categoryGroupId, string userId, string name, CancellationToken cancellationToken = default)
    {
        var group = await _context.CategoryGroups
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);

        if (group == null)
            return false;

        group.Name = name;
        group.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCategoryGroupAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default)
    {
        var group = await _context.CategoryGroups
            .Include(cg => cg.Categories)
            .FirstOrDefaultAsync(cg => cg.Id == categoryGroupId && cg.UserId == userId, cancellationToken);

        if (group == null)
            return false;

        // Check if group has active categories
        if (group.Categories.Any(c => c.IsActive))
            throw new InvalidOperationException("Cannot delete category group with active categories. Move or delete categories first.");

        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderCategoryGroupsAsync(string userId, List<int> orderedGroupIds, CancellationToken cancellationToken = default)
    {
        var groups = await _context.CategoryGroups
            .Where(cg => cg.UserId == userId && orderedGroupIds.Contains(cg.Id))
            .ToListAsync(cancellationToken);

        if (groups.Count != orderedGroupIds.Count)
            return false;

        for (int i = 0; i < orderedGroupIds.Count; i++)
        {
            var group = groups.First(cg => cg.Id == orderedGroupIds[i]);
            group.SortOrder = i + 1;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderCategoriesAsync(string userId, int categoryGroupId, List<int> orderedCategoryIds, CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .Where(c => c.UserId == userId && c.CategoryGroupId == categoryGroupId && orderedCategoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (categories.Count != orderedCategoryIds.Count)
            return false;

        for (int i = 0; i < orderedCategoryIds.Count; i++)
        {
            var category = categories.First(c => c.Id == orderedCategoryIds[i]);
            category.SortOrder = i + 1;
            category.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Category> CreateCategoryAsync(string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await _context.Categories
            .Where(c => c.UserId == userId && c.CategoryGroupId == categoryGroupId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        var category = new Category
        {
            UserId = userId,
            Name = name,
            CategoryGroupId = categoryGroupId,
            SortOrder = maxSortOrder + 1
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return category;
    }

    public async Task<bool> UpdateCategoryAsync(int categoryId, string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.Name = name;
        category.CategoryGroupId = categoryGroupId;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SeedDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Check if user already has categories
        var hasCategories = await _context.Categories.AnyAsync(c => c.UserId == userId, cancellationToken);
        if (hasCategories)
            return;

        // Create default category groups and categories
        var defaults = new Dictionary<string, string[]>
        {
            { "Income", new[] {  "Salary", "Freelance", "Investments", "Other Income" } },
            { "Housing", new[] { "Rent/Mortgage", "Utilities", "Maintenance", "Insurance" } },
            { "Transportation", new[] { "Gas/Fuel", "Public Transit", "Parking", "Car Payment", "Maintenance" } },
            { "Food", new[] { "Groceries", "Restaurants", "Coffee/Snacks" } },
            { "Shopping", new[] { "Clothing", "Electronics", "Home Goods", "Other Shopping" } },
            { "Entertainment", new[] { "Subscriptions", "Movies/Events", "Hobbies" } },
            { "Health", new[] { "Medical", "Pharmacy", "Fitness", "Insurance" } },
            { "Personal", new[] { "Personal Care", "Education", "Gifts" } },
            { "Miscellaneous", new[] { "Fees/Charges", "Taxes", "Uncategorized" } }
        };

        int groupOrder = 1;
        foreach (var (groupName, categories) in defaults)
        {
            var group = new CategoryGroup
            {
                UserId = userId,
                Name = groupName,
                SortOrder = groupOrder++
            };
            _context.CategoryGroups.Add(group);
            await _context.SaveChangesAsync(cancellationToken);

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
                _context.Categories.Add(category);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<MonthlyBudget?> GetEffectiveBudgetAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        // First check for month-specific override
        var monthOverride = await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && 
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (monthOverride != null)
            return monthOverride;

        // Fall back to default budget
        return await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);
    }

    public async Task<MonthlyBudget?> GetDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);
    }

    public async Task<MonthlyBudget?> GetMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && 
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);
    }

    public async Task<List<MonthlyBudget>> GetEffectiveBudgetsForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        // Get all month-specific overrides
        var overrides = await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && !mb.IsDefault && mb.Year == year && mb.Month == month)
            .ToListAsync(cancellationToken);

        var overrideCategoryIds = overrides.Select(o => o.CategoryId).ToHashSet();

        // Get default budgets for categories without overrides
        var defaults = await _context.MonthlyBudgets
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

    public async Task<List<MonthlyBudget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && mb.IsDefault)
            .OrderBy(mb => mb.Category.CategoryGroup.SortOrder)
            .ThenBy(mb => mb.Category.CategoryGroup.Name)
            .ThenBy(mb => mb.Category.SortOrder)
            .ThenBy(mb => mb.Category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MonthlyBudget>> GetMonthOverridesAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _context.MonthlyBudgets
            .Include(mb => mb.Category)
            .ThenInclude(c => c.CategoryGroup)
            .Where(mb => mb.UserId == userId && !mb.IsDefault && mb.Year == year && mb.Month == month)
            .OrderBy(mb => mb.Category.CategoryGroup.SortOrder)
            .ThenBy(mb => mb.Category.CategoryGroup.Name)
            .ThenBy(mb => mb.Category.SortOrder)
            .ThenBy(mb => mb.Category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MonthlyBudget> SetDefaultBudgetAsync(int categoryId, string userId, decimal plannedAmount, bool allowRollover = false, CancellationToken cancellationToken = default)
    {
        // Verify the category belongs to the user
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        // Check if default budget already exists
        var existingBudget = await _context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);

        if (existingBudget != null)
        {
            // Update existing default budget
            existingBudget.PlannedAmount = plannedAmount;
            existingBudget.AllowRollover = allowRollover;
            existingBudget.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existingBudget;
        }
        else
        {
            // Create new default budget
            var newBudget = new MonthlyBudget
            {
                UserId = userId,
                CategoryId = categoryId,
                IsDefault = true,
                Year = null,
                Month = null,
                PlannedAmount = plannedAmount,
                AllowRollover = allowRollover
            };

            _context.MonthlyBudgets.Add(newBudget);
            await _context.SaveChangesAsync(cancellationToken);
            return newBudget;
        }
    }

    public async Task<MonthlyBudget> SetMonthOverrideAsync(int categoryId, string userId, int year, int month, decimal plannedAmount, bool allowRollover = false, CancellationToken cancellationToken = default)
    {
        // Verify the category belongs to the user
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            throw new InvalidOperationException("Category not found or access denied");

        // Check if override already exists
        var existingBudget = await _context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && 
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (existingBudget != null)
        {
            // Update existing override
            existingBudget.PlannedAmount = plannedAmount;
            existingBudget.AllowRollover = allowRollover;
            existingBudget.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existingBudget;
        }
        else
        {
            // Create new override
            var newBudget = new MonthlyBudget
            {
                UserId = userId,
                CategoryId = categoryId,
                IsDefault = false,
                Year = year,
                Month = month,
                PlannedAmount = plannedAmount,
                AllowRollover = allowRollover
            };

            _context.MonthlyBudgets.Add(newBudget);
            await _context.SaveChangesAsync(cancellationToken);
            return newBudget;
        }
    }

    public async Task<bool> DeleteDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default)
    {
        var budget = await _context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && mb.IsDefault, cancellationToken);

        if (budget == null)
            return false;

        _context.MonthlyBudgets.Remove(budget);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var budget = await _context.MonthlyBudgets
            .FirstOrDefaultAsync(mb => mb.CategoryId == categoryId && mb.UserId == userId && 
                                      !mb.IsDefault && mb.Year == year && mb.Month == month, cancellationToken);

        if (budget == null)
            return false;

        _context.MonthlyBudgets.Remove(budget);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RenameCategoryAsync(int categoryId, string userId, string newName, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);

        if (category == null)
            return false;

        // Check if new name already exists in the same group
        var nameExists = await _context.Categories
            .AnyAsync(c => c.UserId == userId && c.CategoryGroupId == category.CategoryGroupId && 
                          c.Name == newName && c.Id != categoryId && c.IsActive, cancellationToken);

        if (nameExists)
            throw new InvalidOperationException($"A category named '{newName}' already exists in this group");

        category.Name = newName;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MergeCategoriesAsync(int sourceCategoryId, int targetCategoryId, string userId, CancellationToken cancellationToken = default)
    {
        if (sourceCategoryId == targetCategoryId)
            throw new InvalidOperationException("Cannot merge a category into itself");

        // Verify both categories exist and belong to user
        var sourceCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == sourceCategoryId && c.UserId == userId, cancellationToken);

        var targetCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == targetCategoryId && c.UserId == userId, cancellationToken);

        if (sourceCategory == null || targetCategory == null)
            throw new InvalidOperationException("Source or target category not found");

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Update all transactions from source to target
            var transactions = await _context.Transactions
                .Where(t => t.CategoryId == sourceCategoryId && t.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var txn in transactions)
            {
                txn.CategoryId = targetCategoryId;
                txn.UpdatedAt = DateTime.UtcNow;
            }

            // Update all transaction splits from source to target
            var splits = await _context.TransactionSplits
                .Include(ts => ts.Transaction)
                .Where(ts => ts.CategoryId == sourceCategoryId && ts.Transaction.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var split in splits)
            {
                split.CategoryId = targetCategoryId;
            }

            // Handle monthly budgets - merge or delete
            var sourceBudgets = await _context.MonthlyBudgets
                .Where(mb => mb.CategoryId == sourceCategoryId && mb.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var sourceBudget in sourceBudgets)
            {
                // Check if target already has a budget for the same period
                var targetBudget = await _context.MonthlyBudgets
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
                    _context.MonthlyBudgets.Remove(sourceBudget);
                }
            }

            // Handle named budget categories
            var sourceBudgetCategories = await _context.BudgetCategories
                .Where(bc => bc.CategoryId == sourceCategoryId)
                .Include(bc => bc.Budget)
                .Where(bc => bc.Budget.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var sourceBudgetCategory in sourceBudgetCategories)
            {
                // Check if target already has this budget assignment
                var targetBudgetCategory = await _context.BudgetCategories
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
                    _context.BudgetCategories.Remove(sourceBudgetCategory);
                }
            }

            // Soft-delete the source category
            sourceCategory.IsActive = false;
            sourceCategory.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Budgets;

public class BudgetService(ApplicationDbContext context) : IBudgetService
{
    public async Task<List<Budget>> GetBudgetsAsync(string userId)
    {
        return await context.Budgets
            .Where(b => b.UserId == userId)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<Budget>> GetDefaultBudgetsAsync(string userId)
    {
        return await context.Budgets
            .Where(b => b.UserId == userId && b.IsDefault)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<Budget?> GetBudgetByIdAsync(int budgetId, string userId)
    {
        return await context.Budgets
            .Where(b => b.Id == budgetId && b.UserId == userId)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Budget>> GetBudgetsForPeriodAsync(string userId, int year, int month)
    {
        return await context.Budgets
            .Where(b => b.UserId == userId && !b.IsDefault && b.Year == year && b.Month == month)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<Budget?> GetEffectiveBudgetAsync(string userId, string budgetName, int year, int month)
    {
        // First check for period-specific budget
        var periodSpecific = await context.Budgets
            .Where(b => b.UserId == userId && b.Name == budgetName && !b.IsDefault && b.Year == year && b.Month == month)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .FirstOrDefaultAsync();

        if (periodSpecific != null)
            return periodSpecific;

        // Fall back to default budget
        return await context.Budgets
            .Where(b => b.UserId == userId && b.Name == budgetName && b.IsDefault)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup)
            .FirstOrDefaultAsync();
    }

    public async Task<Budget> CreateBudgetAsync(string userId, string name, string? description, bool isDefault, int? year, int? month)
    {
        // Validate: if not default, year and month are required
        if (!isDefault && (year == null || month == null))
            throw new InvalidOperationException("Year and month are required for period-specific budgets");

        var budget = new Budget
        {
            UserId = userId,
            Name = name,
            Description = description,
            IsDefault = isDefault,
            Year = year,
            Month = month,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Budgets.Add(budget);
        await context.SaveChangesAsync();

        return budget;
    }

    public async Task<Budget> UpdateBudgetAsync(int budgetId, string userId, string name, string? description, bool isDefault, int? year, int? month)
    {
        var budget = await context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);

        if (budget == null)
            throw new InvalidOperationException("Budget not found");

        // Validate: if not default, year and month are required
        if (!isDefault && (year == null || month == null))
            throw new InvalidOperationException("Year and month are required for period-specific budgets");

        budget.Name = name;
        budget.Description = description;
        budget.IsDefault = isDefault;
        budget.Year = year;
        budget.Month = month;
        budget.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return budget;
    }

    public async Task<bool> DeleteBudgetAsync(int budgetId, string userId)
    {
        var budget = await context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);

        if (budget == null)
            return false;

        context.Budgets.Remove(budget);
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<BudgetCategory> AddCategoryToBudgetAsync(int budgetId, string userId, int categoryId, decimal plannedAmount, bool allowRollover, string? notes = null)
    {
        // Verify budget belongs to user
        var budget = await context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId);

        if (budget == null)
            throw new InvalidOperationException("Budget not found");

        // Check if category already exists in budget
        var existing = await context.BudgetCategories
            .FirstOrDefaultAsync(bc => bc.BudgetId == budgetId && bc.CategoryId == categoryId);

        if (existing != null)
            throw new InvalidOperationException("Category already exists in this budget");

        var budgetCategory = new BudgetCategory
        {
            BudgetId = budgetId,
            CategoryId = categoryId,
            PlannedAmount = plannedAmount,
            AllowRollover = allowRollover,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.BudgetCategories.Add(budgetCategory);
        
        budget.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();

        // Load the category for return
        await context.Entry(budgetCategory)
            .Reference(bc => bc.Category)
            .LoadAsync();

        return budgetCategory;
    }

    public async Task<BudgetCategory> UpdateBudgetCategoryAsync(int budgetCategoryId, string userId, decimal plannedAmount, bool allowRollover, string? notes = null)
    {
        var budgetCategory = await context.BudgetCategories
            .Include(bc => bc.Budget)
            .FirstOrDefaultAsync(bc => bc.Id == budgetCategoryId && bc.Budget.UserId == userId);

        if (budgetCategory == null)
            throw new InvalidOperationException("Budget category not found");

        budgetCategory.PlannedAmount = plannedAmount;
        budgetCategory.AllowRollover = allowRollover;
        budgetCategory.Notes = notes;
        budgetCategory.UpdatedAt = DateTime.UtcNow;
        
        budgetCategory.Budget.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return budgetCategory;
    }

    public async Task<bool> RemoveCategoryFromBudgetAsync(int budgetCategoryId, string userId)
    {
        var budgetCategory = await context.BudgetCategories
            .Include(bc => bc.Budget)
            .FirstOrDefaultAsync(bc => bc.Id == budgetCategoryId && bc.Budget.UserId == userId);

        if (budgetCategory == null)
            return false;

        context.BudgetCategories.Remove(budgetCategory);
        
        budgetCategory.Budget.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<decimal> GetTotalBudgetedAsync(int budgetId, string userId)
    {
        var budget = await context.Budgets
            .Where(b => b.Id == budgetId && b.UserId == userId)
            .Include(b => b.BudgetCategories)
            .FirstOrDefaultAsync();

        if (budget == null)
            return 0;

        return budget.BudgetCategories.Sum(bc => bc.PlannedAmount);
    }
}

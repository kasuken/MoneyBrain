using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyBrain.Web.Application.Common;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Budgets;

public class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<BudgetService> _logger;

    public BudgetService(ApplicationDbContext context, ICacheService cacheService, ILogger<BudgetService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    private static IQueryable<Budget> IncludeBudgetCategoryDetails(IQueryable<Budget> query)
    {
        return query
            .AsNoTracking()
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
                    .ThenInclude(c => c.CategoryGroup);
    }

    public async Task<List<Budget>> GetBudgetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId))
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Budget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId).Where(b => b.IsDefault))
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget?> GetBudgetByIdAsync(int budgetId, string userId, CancellationToken cancellationToken = default)
    {
        return await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId).Where(b => b.Id == budgetId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Budget>> GetBudgetsForPeriodAsync(string userId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId).Where(b => !b.IsDefault && b.Year == year && b.Month == month))
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget?> GetEffectiveBudgetAsync(string userId, string budgetName, int year, int month, CancellationToken cancellationToken = default)
    {
        // First check for period-specific budget
        var periodSpecific = await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId).Where(b => b.Name == budgetName && !b.IsDefault && b.Year == year && b.Month == month))
            .FirstOrDefaultAsync(cancellationToken);

        if (periodSpecific != null)
            return periodSpecific;

        // Fall back to default budget
        return await IncludeBudgetCategoryDetails(
            _context.Budgets.ForUser(userId).Where(b => b.Name == budgetName && b.IsDefault))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Budget> CreateBudgetAsync(string userId, string name, string? description, bool isDefault, int? year, int? month, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budget);

        return budget;
    }

    public async Task<Budget> UpdateBudgetAsync(int budgetId, string userId, string name, string? description, bool isDefault, int? year, int? month, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId, cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budget);

        return budget;
    }

    public async Task<bool> DeleteBudgetAsync(int budgetId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId, cancellationToken);

        if (budget == null)
            return false;

        _context.Budgets.Remove(budget);
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budget);

        return true;
    }

    public async Task<BudgetCategory> AddCategoryToBudgetAsync(int budgetId, string userId, int categoryId, decimal plannedAmount, bool allowRollover, string? notes = null, CancellationToken cancellationToken = default)
    {
        // Verify budget belongs to user
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId, cancellationToken);

        if (budget == null)
            throw new InvalidOperationException("Budget not found");

        // Check if category already exists in budget
        var existing = await _context.BudgetCategories
            .FirstOrDefaultAsync(bc => bc.BudgetId == budgetId && bc.CategoryId == categoryId, cancellationToken);

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

        _context.BudgetCategories.Add(budgetCategory);
        
        budget.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budget);

        // Load the category for return
        await _context.Entry(budgetCategory)
            .Reference(bc => bc.Category)
            .LoadAsync(cancellationToken);

        return budgetCategory;
    }

    public async Task<BudgetCategory> UpdateBudgetCategoryAsync(int budgetCategoryId, string userId, decimal plannedAmount, bool allowRollover, string? notes = null, CancellationToken cancellationToken = default)
    {
        var budgetCategory = await _context.BudgetCategories
            .Include(bc => bc.Budget)
            .FirstOrDefaultAsync(bc => bc.Id == budgetCategoryId && bc.Budget.UserId == userId, cancellationToken);

        if (budgetCategory == null)
            throw new InvalidOperationException("Budget category not found");

        budgetCategory.PlannedAmount = plannedAmount;
        budgetCategory.AllowRollover = allowRollover;
        budgetCategory.Notes = notes;
        budgetCategory.UpdatedAt = DateTime.UtcNow;
        
        budgetCategory.Budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budgetCategory.Budget);

        return budgetCategory;
    }

    public async Task<bool> RemoveCategoryFromBudgetAsync(int budgetCategoryId, string userId, CancellationToken cancellationToken = default)
    {
        var budgetCategory = await _context.BudgetCategories
            .Include(bc => bc.Budget)
            .FirstOrDefaultAsync(bc => bc.Id == budgetCategoryId && bc.Budget.UserId == userId, cancellationToken);

        if (budgetCategory == null)
            return false;

        _context.BudgetCategories.Remove(budgetCategory);
        
        budgetCategory.Budget.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        await InvalidateBudgetComparisonCacheAsync(userId, budgetCategory.Budget);

        return true;
    }

    public async Task<decimal> GetTotalBudgetedAsync(int budgetId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.BudgetCategories
            .Where(bc => bc.BudgetId == budgetId && bc.Budget.UserId == userId)
            .SumAsync(bc => bc.PlannedAmount, cancellationToken);
    }

    public async Task<Budget> CreateBudgetFromTemplateAsync(string userId, string templateName, int year, int month, CancellationToken cancellationToken = default)
    {
        // Get template allocations
        var templateAllocations = GetTemplateAllocations(templateName);
        if (templateAllocations == null)
            throw new InvalidOperationException($"Template '{templateName}' not found");

        // Create budget for the period
        var budget = await CreateBudgetAsync(userId, templateName, $"Budget created from {templateName} template", false, year, month, cancellationToken);

        // Get user's categories to match template allocations
        var userCategories = await _context.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync(cancellationToken);

        // Add categories to budget
        foreach (var (categoryName, amount) in templateAllocations)
        {
            var category = userCategories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (category != null)
            {
                try
                {
                    await AddCategoryToBudgetAsync(budget.Id, userId, category.Id, amount, false, cancellationToken: cancellationToken);
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true || 
                                                     ex.InnerException?.Message.Contains("UNIQUE") == true)
                {
                    // Skip if category already exists in budget (duplicate constraint violation)
                    _logger.LogDebug("Category {CategoryId} already exists in budget {BudgetId}, skipping", category.Id, budget.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error adding category {CategoryId} to budget {BudgetId} from template", category.Id, budget.Id);
                    throw;
                }
            }
        }

        return budget;
    }

    private Dictionary<string, decimal>? GetTemplateAllocations(string templateName)
    {
        var templates = new Dictionary<string, Dictionary<string, decimal>>
        {
            ["Basic Essentials"] = new()
            {
                ["Housing"] = 900m,
                ["Food"] = 500m,
                ["Transportation"] = 300m,
                ["Utilities"] = 300m,
                ["Personal"] = 200m
            },
            ["Standard Living"] = new()
            {
                ["Housing"] = 1400m,
                ["Food"] = 700m,
                ["Transportation"] = 500m,
                ["Shopping"] = 400m,
                ["Entertainment"] = 300m,
                ["Health"] = 300m,
                ["Personal"] = 200m
            },
            ["Savings Focused"] = new()
            {
                ["Housing"] = 1200m,
                ["Food"] = 600m,
                ["Transportation"] = 400m,
                ["Savings"] = 600m,
                ["Health"] = 250m,
                ["Personal"] = 150m
            },
            ["Family Budget"] = new()
            {
                ["Housing"] = 2000m,
                ["Food"] = 1200m,
                ["Transportation"] = 700m,
                ["Education"] = 500m,
                ["Health"] = 500m,
                ["Entertainment"] = 400m,
                ["Shopping"] = 200m
            },
            ["Student Budget"] = new()
            {
                ["Housing"] = 700m,
                ["Food"] = 400m,
                ["Transportation"] = 200m,
                ["Education"] = 300m,
                ["Personal"] = 200m
            }
        };

        return templates.TryGetValue(templateName, out var template) ? template : null;
    }

    private async Task InvalidateBudgetComparisonCacheAsync(string userId, Budget budget)
    {
        if (!budget.IsDefault && budget.Year.HasValue && budget.Month.HasValue)
        {
            var cacheKey = CacheKeyHelper.ForBudgetComparison(userId, budget.Year.Value, budget.Month.Value);
            await _cacheService.RemoveAsync(cacheKey);
        }
        else
        {
            await _cacheService.RemoveByPatternAsync($"user:{userId}:budgetcomparison:*");
        }
    }
}

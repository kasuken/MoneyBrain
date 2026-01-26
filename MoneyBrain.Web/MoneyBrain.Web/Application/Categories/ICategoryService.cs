using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Categories;

/// <summary>
/// Service for managing categories and category groups
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Get all category groups for a user
    /// </summary>
    Task<List<CategoryGroup>> GetCategoryGroupsAsync(string userId, bool includeCategories = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all categories for a user
    /// </summary>
    Task<List<Category>> GetCategoriesAsync(string userId, bool includeInactive = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific category by ID
    /// </summary>
    Task<Category?> GetCategoryByIdAsync(int categoryId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new category group
    /// </summary>
    Task<CategoryGroup> CreateCategoryGroupAsync(string userId, string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing category group
    /// </summary>
    Task<bool> UpdateCategoryGroupAsync(int categoryGroupId, string userId, string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a category group (soft delete - mark as inactive)
    /// Categories in the group must be moved or deleted first
    /// </summary>
    Task<bool> DeleteCategoryGroupAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reorder category groups
    /// </summary>
    Task<bool> ReorderCategoryGroupsAsync(string userId, List<int> orderedGroupIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reorder categories within a group
    /// </summary>
    Task<bool> ReorderCategoriesAsync(string userId, int categoryGroupId, List<int> orderedCategoryIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get category group by ID
    /// </summary>
    Task<CategoryGroup?> GetCategoryGroupByIdAsync(int categoryGroupId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new category
    /// </summary>
    Task<Category> CreateCategoryAsync(string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing category
    /// </summary>
    Task<bool> UpdateCategoryAsync(int categoryId, string userId, string name, int categoryGroupId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a category (soft delete - mark as inactive)
    /// </summary>
    Task<bool> DeleteCategoryAsync(int categoryId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Seed default categories for a new user
    /// </summary>
    Task SeedDefaultCategoriesAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get effective budget for a specific category and period (checks override first, then default)
    /// </summary>
    Task<MonthlyBudget?> GetEffectiveBudgetAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get default budget for a category
    /// </summary>
    Task<MonthlyBudget?> GetDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get month-specific override budget for a category
    /// </summary>
    Task<MonthlyBudget?> GetMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all effective budgets for a specific month (defaults + overrides)
    /// </summary>
    Task<List<MonthlyBudget>> GetEffectiveBudgetsForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all default budgets for the user
    /// </summary>
    Task<List<MonthlyBudget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all month-specific overrides
    /// </summary>
    Task<List<MonthlyBudget>> GetMonthOverridesAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set default budget for a category (applies to all months unless overridden)
    /// </summary>
    Task<MonthlyBudget> SetDefaultBudgetAsync(int categoryId, string userId, decimal plannedAmount, bool allowRollover = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set month-specific budget override for a category
    /// </summary>
    Task<MonthlyBudget> SetMonthOverrideAsync(int categoryId, string userId, int year, int month, decimal plannedAmount, bool allowRollover = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete default budget
    /// </summary>
    Task<bool> DeleteDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete month-specific override (reverts to default)
    /// </summary>
    Task<bool> DeleteMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);
}

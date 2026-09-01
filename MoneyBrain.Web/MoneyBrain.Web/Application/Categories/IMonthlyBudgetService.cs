using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Categories;

/// <summary>
/// Service for managing per-category monthly budgets (defaults and month-specific overrides).
/// </summary>
public interface IMonthlyBudgetService
{
    /// <summary>
    /// Get effective budget for a specific category and period (checks override first, then default).
    /// </summary>
    Task<MonthlyBudget?> GetEffectiveBudgetAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get default budget for a category.
    /// </summary>
    Task<MonthlyBudget?> GetDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get month-specific override budget for a category.
    /// </summary>
    Task<MonthlyBudget?> GetMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all effective budgets for a specific month (defaults + overrides).
    /// </summary>
    Task<List<MonthlyBudget>> GetEffectiveBudgetsForMonthAsync(string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all default budgets for the user.
    /// </summary>
    Task<List<MonthlyBudget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all month-specific overrides.
    /// </summary>
    Task<List<MonthlyBudget>> GetMonthOverridesAsync(string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set default budget for a category (applies to all months unless overridden).
    /// </summary>
    Task<MonthlyBudget> SetDefaultBudgetAsync(int categoryId, string userId, decimal plannedAmount, bool allowRollover = false, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set month-specific budget override for a category.
    /// </summary>
    Task<MonthlyBudget> SetMonthOverrideAsync(int categoryId, string userId, int year, int month, decimal plannedAmount, bool allowRollover = false, string? notes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete default budget.
    /// </summary>
    Task<bool> DeleteDefaultBudgetAsync(int categoryId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete month-specific override (reverts to default).
    /// </summary>
    Task<bool> DeleteMonthOverrideAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get actual spending for a category in a specific month (expenses only, positive value).
    /// </summary>
    Task<decimal> GetCategoryActualSpendingAsync(int categoryId, string userId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get actual spending for all categories in a specific month (returns dictionary of categoryId -> spending amount).
    /// </summary>
    Task<Dictionary<int, decimal>> GetAllCategoriesActualSpendingAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
}

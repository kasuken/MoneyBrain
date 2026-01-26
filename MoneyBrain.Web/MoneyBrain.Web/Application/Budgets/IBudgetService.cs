using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Budgets;

public interface IBudgetService
{
    // Budget CRUD operations
    Task<List<Budget>> GetBudgetsAsync(string userId);
    Task<List<Budget>> GetDefaultBudgetsAsync(string userId);
    Task<Budget?> GetBudgetByIdAsync(int budgetId, string userId);
    Task<List<Budget>> GetBudgetsForPeriodAsync(string userId, int year, int month);
    Task<Budget?> GetEffectiveBudgetAsync(string userId, string budgetName, int year, int month);
    Task<Budget> CreateBudgetAsync(string userId, string name, string? description, bool isDefault, int? year, int? month);
    Task<Budget> UpdateBudgetAsync(int budgetId, string userId, string name, string? description, bool isDefault, int? year, int? month);
    Task<bool> DeleteBudgetAsync(int budgetId, string userId);
    
    // Budget category operations
    Task<BudgetCategory> AddCategoryToBudgetAsync(int budgetId, string userId, int categoryId, decimal plannedAmount, bool allowRollover, string? notes = null);
    Task<BudgetCategory> UpdateBudgetCategoryAsync(int budgetCategoryId, string userId, decimal plannedAmount, bool allowRollover, string? notes = null);
    Task<bool> RemoveCategoryFromBudgetAsync(int budgetCategoryId, string userId);
    
    // Reporting
    Task<decimal> GetTotalBudgetedAsync(int budgetId, string userId);
}

using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Budgets;

public interface IBudgetService
{
    // Budget CRUD operations
    Task<List<Budget>> GetBudgetsAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<Budget>> GetDefaultBudgetsAsync(string userId, CancellationToken cancellationToken = default);
    Task<Budget?> GetBudgetByIdAsync(int budgetId, string userId, CancellationToken cancellationToken = default);
    Task<List<Budget>> GetBudgetsForPeriodAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
    Task<Budget?> GetEffectiveBudgetAsync(string userId, string budgetName, int year, int month, CancellationToken cancellationToken = default);
    Task<Budget> CreateBudgetAsync(string userId, string name, string? description, bool isDefault, int? year, int? month, CancellationToken cancellationToken = default);
    Task<Budget> UpdateBudgetAsync(int budgetId, string userId, string name, string? description, bool isDefault, int? year, int? month, CancellationToken cancellationToken = default);
    Task<bool> DeleteBudgetAsync(int budgetId, string userId, CancellationToken cancellationToken = default);
    
    // Budget category operations
    Task<BudgetCategory> AddCategoryToBudgetAsync(int budgetId, string userId, int categoryId, decimal plannedAmount, bool allowRollover, string? notes = null, CancellationToken cancellationToken = default);
    Task<BudgetCategory> UpdateBudgetCategoryAsync(int budgetCategoryId, string userId, decimal plannedAmount, bool allowRollover, string? notes = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveCategoryFromBudgetAsync(int budgetCategoryId, string userId, CancellationToken cancellationToken = default);
    
    // Reporting
    Task<decimal> GetTotalBudgetedAsync(int budgetId, string userId, CancellationToken cancellationToken = default);
    
    // Templates
    Task<Budget> CreateBudgetFromTemplateAsync(string userId, string templateName, int year, int month, CancellationToken cancellationToken = default);
}

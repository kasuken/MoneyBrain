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
}

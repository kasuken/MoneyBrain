using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Category group for organizing categories
/// </summary>
public class CategoryGroup : IUserOwnedEntity
{
    public int Id { get; init; }
    
    public required string UserId { get; init; }
    
    public required string Name { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Defines whether categories in this group represent income or expenses
    /// </summary>
    public CategoryType Type { get; set; } = CategoryType.Expense;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<Category> Categories { get; set; } = [];
}

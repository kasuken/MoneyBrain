namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Category group for organizing categories
/// </summary>
public class CategoryGroup
{
    public int Id { get; set; }
    
    public required string UserId { get; set; }
    
    public required string Name { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    public int SortOrder { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<Category> Categories { get; set; } = [];
}

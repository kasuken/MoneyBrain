using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Transaction category - must belong to exactly one group
/// </summary>
public class Category : IUserOwnedEntity
{
    public int Id { get; init; }
    
    public required string UserId { get; init; }
    
    public required string Name { get; set; }
    
    public int CategoryGroupId { get; set; }
    
    /// <summary>
    /// Display order within group
    /// </summary>
    public int SortOrder { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public CategoryGroup CategoryGroup { get; set; } = null!;
    
    public ICollection<Transaction> Transactions { get; set; } = [];
}

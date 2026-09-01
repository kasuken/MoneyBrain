using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a named budget containing multiple category allocations for a specific period
/// </summary>
public class Budget : IUserOwnedEntity
{
    public int Id { get; init; }
    public required string UserId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<BudgetCategory> BudgetCategories { get; set; } = [];
}

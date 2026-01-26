namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a named budget containing multiple category allocations for a specific period
/// </summary>
public class Budget
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<BudgetCategory> BudgetCategories { get; set; } = [];
}

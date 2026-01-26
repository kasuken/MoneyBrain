namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a category allocation within a budget
/// </summary>
public class BudgetCategory
{
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public int CategoryId { get; set; }
    public decimal PlannedAmount { get; set; }
    public bool AllowRollover { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public Budget Budget { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Monthly budget (envelope) for a category
/// Can be either a default budget (applies to all months) or a month-specific override
/// </summary>
public class MonthlyBudget : IUserOwnedEntity
{
    public int Id { get; init; }
    
    public required string UserId { get; init; }
    
    public int CategoryId { get; set; }
    
    /// <summary>
    /// Whether this is a default budget that applies to all months
    /// If true, Year and Month are nullable and ignored
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    /// <summary>
    /// Year of the budget period (e.g., 2026)
    /// Nullable for default budgets
    /// </summary>
    public int? Year { get; set; }
    
    /// <summary>
    /// Month of the budget period (1-12)
    /// Nullable for default budgets
    /// </summary>
    public int? Month { get; set; }
    
    /// <summary>
    /// Planned/budgeted amount for this category and period
    /// </summary>
    public decimal PlannedAmount { get; set; }
    
    /// <summary>
    /// Whether to rollover remaining balance from previous month
    /// Default: false
    /// </summary>
    public bool AllowRollover { get; set; } = false;
    
    /// <summary>
    /// Optional notes for this budget entry
    /// </summary>
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Category Category { get; set; } = null!;
}

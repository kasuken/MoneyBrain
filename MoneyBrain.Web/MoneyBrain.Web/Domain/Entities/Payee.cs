using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Payee for transactions (vendor, person, etc.)
/// </summary>
public class Payee : IUserOwnedEntity
{
    public int Id { get; init; }
    
    public required string UserId { get; init; }
    
    public required string Name { get; set; }
    
    public string? Notes { get; set; }
    
    /// <summary>
    /// Default category for this payee (optional)
    /// </summary>
    public int? DefaultCategoryId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Category? DefaultCategory { get; set; }
    
    public ICollection<Transaction> Transactions { get; set; } = [];
}

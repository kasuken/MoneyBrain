namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Split line for a transaction - sum(splits) must equal transaction amount
/// </summary>
public class TransactionSplit
{
    public int Id { get; set; }
    
    public int TransactionId { get; set; }
    
    public int? CategoryId { get; set; }
    
    public decimal Amount { get; set; }
    
    public string? Memo { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Transaction Transaction { get; set; } = null!;
    
    public Category? Category { get; set; }
}

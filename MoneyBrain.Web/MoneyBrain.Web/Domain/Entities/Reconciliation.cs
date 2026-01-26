namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Tracks account reconciliation against statements
/// </summary>
public class Reconciliation
{
    public int Id { get; set; }
    
    public required string UserId { get; set; }
    
    public int AccountId { get; set; }
    
    /// <summary>
    /// Statement ending date
    /// </summary>
    public required DateTime StatementDate { get; set; }
    
    /// <summary>
    /// Statement ending balance
    /// </summary>
    public decimal StatementBalance { get; set; }
    
    /// <summary>
    /// Opening balance at start of reconciliation period
    /// </summary>
    public decimal OpeningBalance { get; set; }
    
    /// <summary>
    /// Calculated reconciled balance (opening + reconciled transactions)
    /// </summary>
    public decimal ReconciledBalance { get; set; }
    
    /// <summary>
    /// Difference between statement and reconciled balance
    /// </summary>
    public decimal Difference { get; set; }
    
    /// <summary>
    /// Whether this reconciliation is complete and locked
    /// </summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// Date reconciliation was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Notes about this reconciliation
    /// </summary>
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Account Account { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

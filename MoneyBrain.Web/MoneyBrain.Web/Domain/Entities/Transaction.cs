using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Transaction - belongs to exactly one account
/// </summary>
public class Transaction
{
    public int Id { get; set; }
    
    public required string UserId { get; set; }
    
    public int AccountId { get; set; }
    
    public required DateTime Date { get; set; }
    
    /// <summary>
    /// Transaction amount (positive for income, negative for expense)
    /// </summary>
    public decimal Amount { get; set; }
    
    public int? PayeeId { get; set; }
    
    public int? CategoryId { get; set; }
    
    public string? Memo { get; set; }
    
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    
    /// <summary>
    /// Cleared flag (transaction has processed but not reconciled)
    /// </summary>
    public bool IsCleared { get; set; }
    
    /// <summary>
    /// Reconciled flag - reconciled transactions are immutable
    /// </summary>
    public bool IsReconciled { get; set; }
    
    /// <summary>
    /// Reconciliation session this transaction belongs to (if reconciled)
    /// </summary>
    public int? ReconciliationId { get; set; }
    
    /// <summary>
    /// Reference number (check number, confirmation, etc.)
    /// </summary>
    public string? ReferenceNumber { get; set; }
    
    /// <summary>
    /// Tags as comma-separated values
    /// </summary>
    public string? Tags { get; set; }
    
    /// <summary>
    /// If this transaction is part of a transfer, link to the other transaction
    /// </summary>
    public int? TransferTransactionId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Account Account { get; set; } = null!;
    
    public Payee? Payee { get; set; }
    
    public Category? Category { get; set; }
    
    public Reconciliation? Reconciliation { get; set; }
    
    public Transaction? TransferTransaction { get; set; }
    
    public ICollection<TransactionSplit> Splits { get; set; } = [];
}

namespace MoneyBrain.Web.Application.Transactions.StatusManagement;

/// <summary>
/// Request to update transaction status in bulk
/// </summary>
public sealed class StatusUpdateRequest
{
    /// <summary>
    /// Transaction IDs to update
    /// </summary>
    public List<int> TransactionIds { get; set; } = [];
    
    /// <summary>
    /// New status to apply
    /// </summary>
    public Domain.Enums.TransactionStatus NewStatus { get; set; }
    
    /// <summary>
    /// Whether to skip reconciled transactions (recommended: true)
    /// </summary>
    public bool SkipReconciled { get; set; } = true;
    
    /// <summary>
    /// Whether to skip transfers (recommended: depends on use case)
    /// </summary>
    public bool SkipTransfers { get; set; } = false;
}

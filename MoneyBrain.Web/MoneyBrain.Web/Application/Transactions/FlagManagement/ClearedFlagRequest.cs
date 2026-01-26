namespace MoneyBrain.Web.Application.Transactions.FlagManagement;

/// <summary>
/// Request to update cleared flag in bulk
/// </summary>
public sealed class ClearedFlagRequest
{
    /// <summary>
    /// Transaction IDs to update
    /// </summary>
    public List<int> TransactionIds { get; set; } = [];
    
    /// <summary>
    /// New cleared flag value
    /// </summary>
    public bool IsCleared { get; set; }
    
    /// <summary>
    /// Whether to skip reconciled transactions (recommended: true)
    /// </summary>
    public bool SkipReconciled { get; set; } = true;
}

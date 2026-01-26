namespace MoneyBrain.Web.Application.Transactions.FlagManagement;

/// <summary>
/// Result of bulk flag update operation
/// </summary>
public sealed class FlagUpdateResult
{
    /// <summary>
    /// Number of transactions updated
    /// </summary>
    public int UpdatedCount { get; set; }
    
    /// <summary>
    /// Transaction IDs that were skipped
    /// </summary>
    public List<int> SkippedTransactionIds { get; set; } = [];
    
    /// <summary>
    /// Reason for skipped transactions
    /// </summary>
    public string? SkipReason { get; set; }
}

namespace MoneyBrain.Web.Application.Transactions.BulkEdit;

/// <summary>
/// Result of a bulk edit operation
/// </summary>
public class BulkEditResult
{
    /// <summary>
    /// Number of transactions successfully updated
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// IDs of transactions that were skipped (e.g., reconciled transactions)
    /// </summary>
    public List<int> SkippedTransactionIds { get; set; } = [];

    /// <summary>
    /// Reason for skipping transactions
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Whether all requested transactions were updated
    /// </summary>
    public bool AllUpdated => SkippedTransactionIds.Count == 0;
}

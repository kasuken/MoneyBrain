namespace MoneyBrain.Web.Application.Transactions.BulkEdit;

/// <summary>
/// Request model for bulk editing multiple transactions.
/// Only non-null fields will be updated.
/// </summary>
public class BulkEditTransactionRequest
{
    /// <summary>
    /// IDs of transactions to update
    /// </summary>
    public List<int> TransactionIds { get; set; } = [];

    /// <summary>
    /// New category ID (null = no change)
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// New payee ID (null = no change)
    /// </summary>
    public int? PayeeId { get; set; }

    /// <summary>
    /// New tags (null = no change, empty list = clear tags)
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Whether to clear the selected field instead of setting it
    /// </summary>
    public bool ClearCategory { get; set; }
    public bool ClearPayee { get; set; }
    public bool ClearTags { get; set; }

    /// <summary>
    /// Checks if any update is specified
    /// </summary>
    public bool HasUpdates => CategoryId.HasValue || PayeeId.HasValue || Tags != null || 
                              ClearCategory || ClearPayee || ClearTags;
}

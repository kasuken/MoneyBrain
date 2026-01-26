namespace MoneyBrain.Web.Application.Transactions.FlagManagement;

/// <summary>
/// Request to update reconciled flag in bulk
/// </summary>
public sealed class ReconciledFlagRequest
{
    /// <summary>
    /// Transaction IDs to update
    /// </summary>
    public List<int> TransactionIds { get; set; } = [];
    
    /// <summary>
    /// New reconciled flag value
    /// </summary>
    public bool IsReconciled { get; set; }
    
    /// <summary>
    /// Allow unrenconciling transactions (setting IsReconciled = false)
    /// This is dangerous and should require explicit confirmation
    /// </summary>
    public bool AllowUnreconcile { get; set; } = false;
}

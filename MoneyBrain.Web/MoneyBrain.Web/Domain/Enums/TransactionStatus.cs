namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Transaction status affecting budget calculations
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Pending transaction - does not affect budgets
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Posted transaction - affects budgets
    /// </summary>
    Posted = 1
}

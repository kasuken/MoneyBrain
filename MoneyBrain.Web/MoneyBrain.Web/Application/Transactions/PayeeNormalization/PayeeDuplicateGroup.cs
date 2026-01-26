using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Transactions.PayeeNormalization;

/// <summary>
/// Represents a group of potentially duplicate payees
/// </summary>
public class PayeeDuplicateGroup
{
    /// <summary>
    /// The normalized key that groups these payees together
    /// </summary>
    public string NormalizedKey { get; set; } = string.Empty;

    /// <summary>
    /// List of payees in this duplicate group
    /// </summary>
    public List<PayeeWithUsage> Payees { get; set; } = new();

    /// <summary>
    /// The suggested primary payee (usually the one with most transactions)
    /// </summary>
    public PayeeWithUsage? SuggestedPrimary => Payees.OrderByDescending(p => p.TransactionCount).FirstOrDefault();

    /// <summary>
    /// Whether this group has potential duplicates
    /// </summary>
    public bool HasDuplicates => Payees.Count > 1;
}

/// <summary>
/// Payee with usage statistics
/// </summary>
public class PayeeWithUsage
{
    /// <summary>
    /// The payee entity
    /// </summary>
    public Payee Payee { get; set; } = null!;

    /// <summary>
    /// Number of transactions using this payee
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Date of last transaction with this payee
    /// </summary>
    public DateTime? LastUsedDate { get; set; }
}

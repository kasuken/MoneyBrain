using System.ComponentModel.DataAnnotations;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents an explicit manual adjustment to an account's balance.
/// These adjustments directly affect the calculated balance and are fully auditable.
/// Use cases: bank fees, interest, corrections, reconciliation differences.
/// </summary>
public class ManualBalanceAdjustment
{
    /// <summary>
    /// Unique identifier for the adjustment.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The account this adjustment applies to.
    /// </summary>
    [Required]
    public required int AccountId { get; set; }

    /// <summary>
    /// Navigation property to the account.
    /// </summary>
    public Account? Account { get; set; }

    /// <summary>
    /// The adjustment amount.
    /// Positive = increase balance, negative = decrease balance.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date when this adjustment takes effect.
    /// Used for calculating point-in-time balances.
    /// </summary>
    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Required description explaining why this adjustment was made.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    /// <summary>
    /// Optional category for grouping adjustments (e.g., "Bank Fee", "Interest", "Correction").
    /// </summary>
    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>
    /// User ID who created this adjustment.
    /// </summary>
    [Required]
    public required string CreatedByUserId { get; set; }

    /// <summary>
    /// When this adjustment record was created in the system.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this adjustment has been reconciled.
    /// Once reconciled, it should be immutable.
    /// </summary>
    public bool IsReconciled { get; set; } = false;

    /// <summary>
    /// When this adjustment was reconciled (if applicable).
    /// </summary>
    public DateTime? ReconciledAt { get; set; }
}

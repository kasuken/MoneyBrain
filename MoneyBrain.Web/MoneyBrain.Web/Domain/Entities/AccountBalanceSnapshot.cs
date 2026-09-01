using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a point-in-time snapshot of an account's balance.
/// Used for tracking balance history and generating reports.
/// </summary>
public class AccountBalanceSnapshot
{
    /// <summary>
    /// Unique identifier for the snapshot.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The account this snapshot belongs to.
    /// </summary>
    [Required]
    public required int AccountId { get; init; }

    /// <summary>
    /// Navigation property to the account.
    /// </summary>
    public Account? Account { get; set; }

    /// <summary>
    /// The date and time this snapshot was taken.
    /// </summary>
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The calculated balance at the time of the snapshot.
    /// Balance = OpeningBalance + sum(all transactions up to SnapshotDate) + adjustments.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Type of snapshot (manual, automatic, reconciliation, etc.).
    /// </summary>
    [Required]
    public required SnapshotType Type { get; set; }

    /// <summary>
    /// Optional note or description for this snapshot.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// User ID who created this snapshot (for manual snapshots).
    /// </summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// When this snapshot record was created in the system.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

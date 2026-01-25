using System.ComponentModel.DataAnnotations;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a historical adjustment to an account's opening balance.
/// Provides audit trail for all opening balance changes.
/// </summary>
public class OpeningBalanceAdjustment
{
    /// <summary>
    /// Unique identifier for the adjustment record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The account whose opening balance was adjusted.
    /// </summary>
    [Required]
    public required int AccountId { get; set; }

    /// <summary>
    /// Navigation property to the account.
    /// </summary>
    public Account? Account { get; set; }

    /// <summary>
    /// The opening balance before this adjustment.
    /// </summary>
    public decimal PreviousBalance { get; set; }

    /// <summary>
    /// The new opening balance after this adjustment.
    /// </summary>
    public decimal NewBalance { get; set; }

    /// <summary>
    /// The amount of the adjustment (NewBalance - PreviousBalance).
    /// Positive = increase, negative = decrease.
    /// </summary>
    public decimal AdjustmentAmount { get; set; }

    /// <summary>
    /// Optional reason or note explaining why the adjustment was made.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// When this adjustment was made.
    /// </summary>
    public DateTime AdjustedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who made the adjustment.
    /// </summary>
    [Required]
    public required string AdjustedByUserId { get; set; }
}

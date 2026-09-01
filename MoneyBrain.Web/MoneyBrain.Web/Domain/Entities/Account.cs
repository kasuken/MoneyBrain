using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a financial account (asset or liability) that tracks money inflows and outflows.
/// Every transaction belongs to exactly one account.
/// </summary>
public class Account : IUserOwnedEntity
{
    /// <summary>
    /// Unique identifier for the account.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// The user who owns this account.
    /// </summary>
    [Required]
    public required string UserId { get; init; }

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Display name for the account (e.g., "Chase Checking", "Amex Card").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// Type of account (Asset or Liability).
    /// </summary>
    [Required]
    public required AccountType Type { get; set; }

    /// <summary>
    /// Specific sub-type for better categorization.
    /// </summary>
    [Required]
    public required AccountSubType SubType { get; set; }

    /// <summary>
    /// Optional grouping label for organizing multiple accounts.
    /// </summary>
    [MaxLength(50)]
    public string? Group { get; set; }

    /// <summary>
    /// The initial balance when the account was created or imported.
    /// Account balance = OpeningBalance + sum(all transactions) + adjustments.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Optional notes or description for the account.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this account is active and should be shown in normal views.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the account was created in MoneyBrain.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the account was last modified.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Currency code (ISO 4217, e.g. "USD", "EUR"). 
    /// Optional in v1; defaults to user's primary currency.
    /// </summary>
    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Optional monthly spending limit for this account.
    /// When set, the Accounts page shows a progress indicator (spent / limit).
    /// </summary>
    public decimal? MonthlySpendingLimit { get; set; }

    /// <summary>
    /// For credit cards: day of month (1-31) when the billing cycle closes.
    /// On this day, pending transactions become posted and a bill is created.
    /// </summary>
    public int? BillingCycleDay { get; set; }

    /// <summary>
    /// For credit cards: the account where the monthly bill transaction will be created.
    /// Typically a checking or savings account.
    /// </summary>
    public int? LinkedPaymentAccountId { get; set; }

    /// <summary>
    /// Navigation property for the linked payment account.
    /// </summary>
    public Account? LinkedPaymentAccount { get; set; }

    /// <summary>
    /// Date when the last billing cycle was processed.
    /// Used to prevent duplicate processing.
    /// </summary>
    public DateTime? LastBillingCycleDate { get; set; }

    /// <summary>
    /// Navigation property for all opening balance adjustments made to this account.
    /// Provides audit trail of opening balance changes.
    /// </summary>
    public ICollection<OpeningBalanceAdjustment> OpeningBalanceAdjustments { get; set; } = [];

    /// <summary>
    /// Navigation property for all balance snapshots taken for this account.
    /// Used for tracking balance history over time.
    /// </summary>
    public ICollection<AccountBalanceSnapshot> BalanceSnapshots { get; set; } = [];

    /// <summary>
    /// Navigation property for all manual balance adjustments made to this account.
    /// These directly affect the calculated balance: Balance = OpeningBalance + Transactions + ManualAdjustments.
    /// </summary>
    public ICollection<ManualBalanceAdjustment> ManualBalanceAdjustments { get; set; } = [];

    /// <summary>
    /// Navigation property for all transactions in this account.
    /// Every transaction belongs to exactly one account.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = [];
}

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Represents a single ledger entry in the double-entry bookkeeping system.
/// Every transaction generates at least two ledger entries (debit and credit) to maintain balance.
/// </summary>
public class LedgerEntry
{
    /// <summary>
    /// Unique identifier for the ledger entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user who owns this ledger entry.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// The transaction that generated this ledger entry.
    /// </summary>
    public int TransactionId { get; set; }

    /// <summary>
    /// The account affected by this entry.
    /// This can be a real account (Asset/Liability) or a virtual category account (Income/Expense).
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// The category this entry is associated with (for income/expense entries).
    /// Null for account-to-account transfers.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Debit amount. Positive value increases Assets and Expenses.
    /// </summary>
    public decimal DebitAmount { get; set; }

    /// <summary>
    /// Credit amount. Positive value increases Liabilities, Income, and Equity.
    /// </summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Entry date (matches transaction date).
    /// </summary>
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Description/memo for this entry.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this entry was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Transaction Transaction { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
}

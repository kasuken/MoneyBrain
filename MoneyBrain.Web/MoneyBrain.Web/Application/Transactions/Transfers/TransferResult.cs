using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Transactions.Transfers;

/// <summary>
/// Result of a transfer operation containing both linked transactions
/// </summary>
public class TransferResult
{
    public Transaction FromTransaction { get; set; } = null!;
    public Transaction ToTransaction { get; set; } = null!;
    public int FromAccountId => FromTransaction.AccountId;
    public int ToAccountId => ToTransaction.AccountId;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Memo { get; set; }
}

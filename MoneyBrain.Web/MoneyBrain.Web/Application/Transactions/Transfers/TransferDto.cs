namespace MoneyBrain.Web.Application.Transactions.Transfers;

/// <summary>
/// DTO for creating or updating a transfer between accounts
/// </summary>
public class TransferDto
{
    public int? Id { get; set; }
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Memo { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsCleared { get; set; }
}

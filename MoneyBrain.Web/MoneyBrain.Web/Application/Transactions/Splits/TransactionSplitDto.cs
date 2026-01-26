namespace MoneyBrain.Web.Application.Transactions.Splits;

/// <summary>
/// DTO for creating or updating a transaction split
/// </summary>
public class TransactionSplitDto
{
    public int? Id { get; set; }
    public int? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string? Memo { get; set; }
}

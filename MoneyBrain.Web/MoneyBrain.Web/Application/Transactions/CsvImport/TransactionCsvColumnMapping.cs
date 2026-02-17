namespace MoneyBrain.Web.Application.Transactions.CsvImport;

using MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Represents the mapping of CSV columns to transaction fields
/// </summary>
public class TransactionCsvColumnMapping
{
    /// <summary>
    /// Column index for transaction date (required)
    /// </summary>
    public int? DateColumn { get; set; }
    
    /// <summary>
    /// Column index for transaction amount (required)
    /// </summary>
    public int? AmountColumn { get; set; }
    
    /// <summary>
    /// Column index for payee/merchant name
    /// </summary>
    public int? PayeeColumn { get; set; }
    
    /// <summary>
    /// Column index for category
    /// </summary>
    public int? CategoryColumn { get; set; }
    
    /// <summary>
    /// Column index for memo/description
    /// </summary>
    public int? MemoColumn { get; set; }
    
    /// <summary>
    /// Column index for reference number/check number
    /// </summary>
    public int? ReferenceNumberColumn { get; set; }
    
    /// <summary>
    /// Column index for cleared status
    /// </summary>
    public int? ClearedColumn { get; set; }
    
    /// <summary>
    /// Whether the CSV file has a header row
    /// </summary>
    public bool HasHeaderRow { get; set; } = true;
    
    /// <summary>
    /// Date format for parsing (e.g., "MM/dd/yyyy", "yyyy-MM-dd")
    /// </summary>
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    
    /// <summary>
    /// Whether amounts are negative for expenses (vs positive)
    /// </summary>
    public bool InvertAmounts { get; set; } = false;
    
    /// <summary>
    /// Default account ID for all imported transactions (required)
    /// </summary>
    public int AccountId { get; set; }
    
    /// <summary>
    /// Default status for imported transactions
    /// </summary>
    public TransactionStatus DefaultStatus { get; set; } = TransactionStatus.Posted;
}

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
    /// Date format for parsing (e.g., "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"). Parsed strictly; "yyyy-MM-dd" is always accepted too.
    /// </summary>
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    
    /// <summary>
    /// Field delimiter. <c>null</c> detects it from the file (comma, semicolon, tab or pipe).
    /// </summary>
    public char? Delimiter { get; set; }
    
    /// <summary>
    /// How amounts are written: decimal point (1,234.56) or decimal comma (1.234,56).
    /// </summary>
    public CsvAmountFormat AmountFormat { get; set; } = CsvAmountFormat.Standard;
    
    /// <summary>
    /// Skip rows that match an existing transaction in the target account (same date, amount and payee).
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;
    
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

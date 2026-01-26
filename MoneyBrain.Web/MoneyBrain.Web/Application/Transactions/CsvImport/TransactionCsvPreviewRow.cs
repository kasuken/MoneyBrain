namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>
/// Represents a preview row from CSV import
/// </summary>
public class TransactionCsvPreviewRow
{
    /// <summary>
    /// Row number in the CSV file (1-indexed)
    /// </summary>
    public int RowNumber { get; set; }
    
    /// <summary>
    /// Original CSV row data
    /// </summary>
    public List<string> RawData { get; set; } = new();
    
    /// <summary>
    /// Parsed transaction date
    /// </summary>
    public DateTime? Date { get; set; }
    
    /// <summary>
    /// Parsed transaction amount
    /// </summary>
    public decimal? Amount { get; set; }
    
    /// <summary>
    /// Parsed payee name
    /// </summary>
    public string? Payee { get; set; }
    
    /// <summary>
    /// Parsed category
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Parsed memo
    /// </summary>
    public string? Memo { get; set; }
    
    /// <summary>
    /// Parsed reference number
    /// </summary>
    public string? ReferenceNumber { get; set; }
    
    /// <summary>
    /// Parsed cleared status
    /// </summary>
    public bool? IsCleared { get; set; }
    
    /// <summary>
    /// List of parsing errors for this row
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Whether this row has any errors
    /// </summary>
    public bool HasErrors => Errors.Count > 0;
}

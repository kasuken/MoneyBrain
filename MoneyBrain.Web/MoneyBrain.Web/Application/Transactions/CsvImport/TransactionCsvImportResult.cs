namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>
/// Result of a CSV import operation
/// </summary>
public class TransactionCsvImportResult
{
    /// <summary>
    /// Number of transactions successfully imported
    /// </summary>
    public int ImportedCount { get; set; }
    
    /// <summary>
    /// Number of transactions skipped due to errors
    /// </summary>
    public int SkippedCount { get; set; }
    
    /// <summary>
    /// Total number of rows processed
    /// </summary>
    public int TotalRows { get; set; }
    
    /// <summary>
    /// List of errors encountered during import
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Whether the import was successful overall
    /// </summary>
    public bool Success => Errors.Count == 0;
}

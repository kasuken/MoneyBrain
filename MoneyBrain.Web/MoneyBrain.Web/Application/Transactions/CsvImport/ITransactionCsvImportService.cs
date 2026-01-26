namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>
/// Service for importing transactions from CSV files
/// </summary>
public interface ITransactionCsvImportService
{
    /// <summary>
    /// Parse CSV content and detect columns
    /// </summary>
    Task<List<List<string>>> ParseCsvAsync(string csvContent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Preview CSV import with current column mapping
    /// </summary>
    Task<List<TransactionCsvPreviewRow>> PreviewImportAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import transactions from CSV
    /// </summary>
    Task<TransactionCsvImportResult> ImportTransactionsAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default);
}

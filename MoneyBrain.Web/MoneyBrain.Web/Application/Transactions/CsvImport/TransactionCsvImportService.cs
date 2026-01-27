using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>
/// Service for importing transactions from CSV files
/// </summary>
public class TransactionCsvImportService : ITransactionCsvImportService
{
    private readonly ApplicationDbContext _context;
    private readonly ITransactionService _transactionService;

    public TransactionCsvImportService(
        ApplicationDbContext context,
        ITransactionService transactionService)
    {
        _context = context;
        _transactionService = transactionService;
    }

    public Task<List<List<string>>> ParseCsvAsync(string csvContent, CancellationToken cancellationToken = default)
    {
        var rows = new List<List<string>>();
        
        using var reader = new StringReader(csvContent);
        string? line;
        
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
                
            var columns = ParseCsvLine(line);
            rows.Add(columns);
        }
        
        return Task.FromResult(rows);
    }

    public async Task<List<TransactionCsvPreviewRow>> PreviewImportAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default)
    {
        var rows = await ParseCsvAsync(csvContent, cancellationToken);
        var previewRows = new List<TransactionCsvPreviewRow>();
        
        var startRow = mapping.HasHeaderRow ? 1 : 0;
        
        for (int i = startRow; i < rows.Count; i++)
        {
            var csvRow = rows[i];
            var previewRow = new TransactionCsvPreviewRow
            {
                RowNumber = i + 1,
                RawData = csvRow
            };
            
            // Parse date
            if (mapping.DateColumn.HasValue && mapping.DateColumn.Value < csvRow.Count)
            {
                var dateText = csvRow[mapping.DateColumn.Value];
                if (DateTime.TryParseExact(dateText, mapping.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    previewRow.Date = date;
                }
                else if (DateTime.TryParse(dateText, out date))
                {
                    previewRow.Date = date;
                }
                else
                {
                    previewRow.Errors.Add($"Invalid date format: {dateText}");
                }
            }
            else
            {
                previewRow.Errors.Add("Date column is required");
            }
            
            // Parse amount
            if (mapping.AmountColumn.HasValue && mapping.AmountColumn.Value < csvRow.Count)
            {
                var amountText = csvRow[mapping.AmountColumn.Value].Replace("$", "").Replace(",", "").Trim();
                if (decimal.TryParse(amountText, out var amount))
                {
                    previewRow.Amount = mapping.InvertAmounts ? -amount : amount;
                }
                else
                {
                    previewRow.Errors.Add($"Invalid amount format: {amountText}");
                }
            }
            else
            {
                previewRow.Errors.Add("Amount column is required");
            }
            
            // Parse payee
            if (mapping.PayeeColumn.HasValue && mapping.PayeeColumn.Value < csvRow.Count)
            {
                previewRow.Payee = csvRow[mapping.PayeeColumn.Value];
            }
            
            // Parse category
            if (mapping.CategoryColumn.HasValue && mapping.CategoryColumn.Value < csvRow.Count)
            {
                previewRow.Category = csvRow[mapping.CategoryColumn.Value];
            }
            
            // Parse memo
            if (mapping.MemoColumn.HasValue && mapping.MemoColumn.Value < csvRow.Count)
            {
                previewRow.Memo = csvRow[mapping.MemoColumn.Value];
            }
            
            // Parse reference number
            if (mapping.ReferenceNumberColumn.HasValue && mapping.ReferenceNumberColumn.Value < csvRow.Count)
            {
                previewRow.ReferenceNumber = csvRow[mapping.ReferenceNumberColumn.Value];
            }
            
            // Parse cleared
            if (mapping.ClearedColumn.HasValue && mapping.ClearedColumn.Value < csvRow.Count)
            {
                var clearedText = csvRow[mapping.ClearedColumn.Value].ToLower();
                previewRow.IsCleared = clearedText == "true" || clearedText == "yes" || clearedText == "x" || clearedText == "1";
            }
            
            previewRows.Add(previewRow);
        }
        
        return previewRows;
    }

    public async Task<TransactionCsvImportResult> ImportTransactionsAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default)
    {
        var result = new TransactionCsvImportResult();
        
        // Validate account
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == mapping.AccountId && a.UserId == userId, cancellationToken);
            
        if (account == null)
        {
            result.Errors.Add("Invalid account ID");
            return result;
        }
        
        // Preview the import
        var previewRows = await PreviewImportAsync(userId, csvContent, mapping, cancellationToken);
        result.TotalRows = previewRows.Count;
        
        // Build category lookup - handle duplicates by taking the first match
        var categoryLookup = (await _context.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync(cancellationToken))
            .GroupBy(c => c.Name.ToLower())
            .ToDictionary(g => g.Key, g => g.First().Id);
        
        // Import transactions
        foreach (var previewRow in previewRows)
        {
            if (previewRow.HasErrors)
            {
                result.SkippedCount++;
                result.Errors.Add($"Row {previewRow.RowNumber}: {string.Join(", ", previewRow.Errors)}");
                continue;
            }
            
            if (!previewRow.Date.HasValue || !previewRow.Amount.HasValue)
            {
                result.SkippedCount++;
                result.Errors.Add($"Row {previewRow.RowNumber}: Missing required fields");
                continue;
            }
            
            try
            {
                // Find or create payee
                int? payeeId = null;
                if (!string.IsNullOrWhiteSpace(previewRow.Payee))
                {
                    var payee = await _transactionService.CreateOrGetPayeeAsync(userId, previewRow.Payee, null, cancellationToken);
                    payeeId = payee.Id;
                }
                
                // Find category
                int? categoryId = null;
                if (!string.IsNullOrWhiteSpace(previewRow.Category) && categoryLookup.TryGetValue(previewRow.Category.ToLower(), out var foundCategoryId))
                {
                    categoryId = foundCategoryId;
                }
                
                // Create transaction
                await _transactionService.CreateTransactionAsync(
                    userId,
                    mapping.AccountId,
                    previewRow.Date.Value,
                    previewRow.Amount.Value,
                    payeeId,
                    categoryId,
                    previewRow.Memo,
                    mapping.DefaultStatus,
                    previewRow.IsCleared ?? false,
                    previewRow.ReferenceNumber,
                    null, // tags
                    isRecurring: false, // CSV imported transactions are non-recurring
                    recurrenceFrequency: null,
                    recurrenceStartDate: null,
                    cancellationToken);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.SkippedCount++;
                result.Errors.Add($"Row {previewRow.RowNumber}: {ex.Message}");
            }
        }
        
        return result;
    }

    private List<string> ParseCsvLine(string line)
    {
        var columns = new List<string>();
        var currentColumn = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentColumn.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                columns.Add(currentColumn.ToString().Trim());
                currentColumn.Clear();
            }
            else
            {
                currentColumn.Append(c);
            }
        }
        
        columns.Add(currentColumn.ToString().Trim());
        return columns;
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.CsvImport;

/// <summary>
/// Service for importing transactions from CSV files
/// </summary>
public class TransactionCsvImportService : ITransactionCsvImportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ITransactionService _transactionService;
    private readonly IPayeeService _payeeService;
    private readonly ILogger<TransactionCsvImportService> _logger;

    public TransactionCsvImportService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ITransactionService transactionService,
        IPayeeService payeeService,
        ILogger<TransactionCsvImportService> logger)
    {
        _contextFactory = contextFactory;
        _transactionService = transactionService;
        _payeeService = payeeService;
        _logger = logger;
    }

    public Task<List<List<string>>> ParseCsvAsync(string csvContent, CancellationToken cancellationToken = default)
        => ParseCsvAsync(csvContent, null, cancellationToken);

    public Task<List<List<string>>> ParseCsvAsync(string csvContent, char? delimiter, CancellationToken cancellationToken = default)
        => Task.FromResult(CsvFormat.Parse(csvContent, delimiter));

    public async Task<List<TransactionCsvPreviewRow>> PreviewImportAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default)
    {
        var rows = await ParseCsvAsync(csvContent, mapping.Delimiter, cancellationToken);
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
                if (CsvFormat.TryParseDate(dateText, mapping.DateFormat, out var date))
                {
                    previewRow.Date = date;
                }
                else
                {
                    previewRow.Errors.Add($"Invalid date (expected {mapping.DateFormat}): {dateText}");
                }
            }
            else
            {
                previewRow.Errors.Add("Date column is required");
            }
            
            // Parse amount
            if (mapping.AmountColumn.HasValue && mapping.AmountColumn.Value < csvRow.Count)
            {
                var amountText = csvRow[mapping.AmountColumn.Value].Trim();
                if (CsvFormat.TryParseAmount(amountText, mapping.AmountFormat, out var amount))
                {
                    previewRow.Amount = mapping.InvertAmounts ? -amount : amount;
                }
                else
                {
                    previewRow.Errors.Add($"Invalid amount (expected {(mapping.AmountFormat == CsvAmountFormat.European ? "1.234,56" : "1,234.56")}): {amountText}");
                }
            }
            else
            {
                previewRow.Errors.Add("Amount column is required");
            }
            
            // Parse payee
            if (mapping.PayeeColumn.HasValue && mapping.PayeeColumn.Value < csvRow.Count)
            {
                previewRow.Payee = CsvFormat.UnescapeText(csvRow[mapping.PayeeColumn.Value]);
            }
            
            // Parse category
            if (mapping.CategoryColumn.HasValue && mapping.CategoryColumn.Value < csvRow.Count)
            {
                previewRow.Category = CsvFormat.UnescapeText(csvRow[mapping.CategoryColumn.Value]);
            }
            
            // Parse memo
            if (mapping.MemoColumn.HasValue && mapping.MemoColumn.Value < csvRow.Count)
            {
                previewRow.Memo = CsvFormat.UnescapeText(csvRow[mapping.MemoColumn.Value]);
            }
            
            // Parse reference number
            if (mapping.ReferenceNumberColumn.HasValue && mapping.ReferenceNumberColumn.Value < csvRow.Count)
            {
                previewRow.ReferenceNumber = CsvFormat.UnescapeText(csvRow[mapping.ReferenceNumberColumn.Value]);
            }
            
            // Parse cleared
            if (mapping.ClearedColumn.HasValue && mapping.ClearedColumn.Value < csvRow.Count)
            {
                var clearedText = csvRow[mapping.ClearedColumn.Value].Trim().ToLowerInvariant();
                previewRow.IsCleared = clearedText == "true" || clearedText == "yes" || clearedText == "x" || clearedText == "1";
            }
            
            previewRows.Add(previewRow);
        }
        
        await MarkDuplicatesAsync(userId, mapping, previewRows, cancellationToken);

        return previewRows;
    }

    public async Task<TransactionCsvImportResult> ImportTransactionsAsync(
        string userId,
        string csvContent,
        TransactionCsvColumnMapping mapping,
        CancellationToken cancellationToken = default)
    {
        var result = new TransactionCsvImportResult();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        // Validate account
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Id == mapping.AccountId && a.UserId == userId, cancellationToken);
            
        if (account == null)
        {
            result.Errors.Add("Invalid account ID");
            return result;
        }
        
        // Preview the import
        var previewRows = await PreviewImportAsync(userId, csvContent, mapping, cancellationToken);
        result.TotalRows = previewRows.Count;
        
        // Build category lookup - project only needed columns to avoid loading full entities.
        // Client-side OrdinalIgnoreCase grouping is intentional: it handles categories that
        // differ only in casing (SQL Server CI collation would surface both rows).
        var categoryLookup = (await context.Categories
            .Where(c => c.UserId == userId && c.IsActive)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        
        // Import transactions
        foreach (var previewRow in previewRows)
        {
            if (previewRow.HasErrors)
            {
                result.SkippedCount++;
                result.Errors.Add($"Row {previewRow.RowNumber}: {string.Join(", ", previewRow.Errors)}");
                continue;
            }
            
            if (mapping.SkipDuplicates && previewRow.IsDuplicate)
            {
                result.SkippedCount++;
                result.DuplicateCount++;
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
                    var payee = await _payeeService.CreateOrGetPayeeAsync(userId, previewRow.Payee, null, cancellationToken);
                    payeeId = payee.Id;
                }
                
                // Find category
                int? categoryId = null;
                if (!string.IsNullOrWhiteSpace(previewRow.Category) && categoryLookup.TryGetValue(previewRow.Category, out var foundCategoryId))
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
                _logger.LogWarning(ex, "CSV import failed for row {RowNumber}", previewRow.RowNumber);
                result.SkippedCount++;
                result.Errors.Add($"Row {previewRow.RowNumber}: {ex.Message}");
            }
        }
        
        return result;
    }

    /// <summary>
    /// Flags rows that match an existing transaction in the target account (same date, amount and payee).
    /// Matching consumes existing transactions one for one, so a file containing two identical purchases is only
    /// a duplicate of two existing ones, and re-importing a file flags every row.
    /// </summary>
    private async Task MarkDuplicatesAsync(
        string userId,
        TransactionCsvColumnMapping mapping,
        List<TransactionCsvPreviewRow> rows,
        CancellationToken cancellationToken)
    {
        var candidates = rows.Where(r => !r.HasErrors && r.Date.HasValue && r.Amount.HasValue).ToList();
        if (candidates.Count == 0)
            return;

        var from = candidates.Min(r => r.Date!.Value.Date);
        var to = candidates.Max(r => r.Date!.Value.Date).AddDays(1);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.AccountId == mapping.AccountId && t.Date >= from && t.Date < to)
            .Select(t => new { t.Date, t.Amount, Payee = t.Payee != null ? t.Payee.Name : null })
            .ToListAsync(cancellationToken);

        var remaining = new Dictionary<(DateTime, decimal, string), int>();
        foreach (var t in existing)
        {
            var key = (t.Date.Date, Math.Abs(t.Amount), NormalizePayee(t.Payee));
            remaining[key] = remaining.GetValueOrDefault(key) + 1;
        }

        foreach (var row in candidates)
        {
            var key = (row.Date!.Value.Date, Math.Abs(row.Amount!.Value), NormalizePayee(row.Payee));
            if (remaining.TryGetValue(key, out var count) && count > 0)
            {
                remaining[key] = count - 1;
                row.IsDuplicate = true;
            }
        }
    }

    private static string NormalizePayee(string? payee) => PayeeNormalizer.GetNormalizedKey(payee ?? string.Empty);
}

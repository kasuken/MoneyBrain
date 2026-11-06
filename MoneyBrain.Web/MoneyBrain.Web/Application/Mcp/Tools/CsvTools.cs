using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Transactions.CsvImport;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Bounded CSV preview/commit and paged transaction export.</summary>
[McpServerToolType]
public sealed class CsvTools(McpAccess access, ITransactionCsvImportService importer, IDbContextFactory<ApplicationDbContext> factory, IMemoryCache cache)
{
    /// <summary>Previews a bounded import and issues an owner-bound, one-use short-lived commit token.</summary>
    [McpServerTool(Name = "csv_import_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview CSV using zero-based column mapping. Maximum 512 KiB and 100 data rows; no embedded newlines in quoted cells. No financial changes. Shows category-normalized amounts. Returns a one-use 10-minute preview token required for commit.")]
    public async Task<CsvPreviewToolDto> Preview(string csvContent, CsvMappingToolInput mapping, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Check(!string.IsNullOrWhiteSpace(csvContent) && Encoding.UTF8.GetByteCount(csvContent) <= 512 * 1024, "CSV must be nonempty and at most 512 KiB.");
        FinanceToolSupport.Validate(mapping);
        ValidateMapping(mapping);
        // The existing application CSV parser is intentionally line-based.
        FinanceToolSupport.Check(csvContent.Split('\n').Length <= 102, "CSV import accepts at most 100 data rows.");
        foreach (var line in csvContent.Split('\n'))
            FinanceToolSupport.Check(line.Count(c => c == '"') % 2 == 0, "Embedded newlines or unbalanced CSV quotes are unsupported.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, mapping.AccountId, user, cancellationToken);
        var parsed = await FinanceToolSupport.Run(() => importer.ParseCsvAsync(csvContent, cancellationToken));
        FinanceToolSupport.Check(parsed.Count > (mapping.HasHeaderRow ? 1 : 0) && parsed.Count <= (mapping.HasHeaderRow ? 101 : 100), "CSV requires between 1 and 100 data rows.");
        FinanceToolSupport.Check(parsed.All(r => r.Count <= 100 && r.All(c => c.Length <= 2000)), "CSV has too many columns or an oversized cell.");
        var rows = await FinanceToolSupport.Run(() => importer.PreviewImportAsync(user, csvContent, Map(mapping), cancellationToken));
        var safeRows = await PreviewRows(db, user, rows, cancellationToken);
        FinanceToolSupport.Check(rows.All(r => r.Memo is null || r.Memo.Length <= 1000) && rows.All(r => r.Payee is null || r.Payee.Length <= 200) &&
            rows.All(r => r.ReferenceNumber is null || r.ReferenceNumber.Length <= 100), "CSV contains fields exceeding transaction limits.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        cache.Set(Key(user, token), new ImportPlan(csvContent, mapping, Fingerprint(safeRows)), TimeSpan.FromMinutes(10));
        return new(token, DateTimeOffset.UtcNow.AddMinutes(10), safeRows, rows.Count(r => r.HasErrors));
    }

    /// <summary>Commits only the exact immutable input stored by a prior owner-bound preview.</summary>
    [McpServerTool(Name = "csv_import_commit", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Commit the exact CSV and mapping from csv_import_preview using its one-use token. Requires confirmed=true. Token consumed before import to prevent duplicate retries; partial failures return counts. After timeout inspect transactions before creating a new preview.")]
    public async Task<CsvImportToolResult> Commit(string previewToken, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        previewToken = FinanceToolSupport.Text(previewToken, "Preview token", 48);
        FinanceToolSupport.Check(cache.TryGetValue<ImportPlan>(Key(user, previewToken), out var plan) && plan is not null, "Preview token is invalid or expired. Run csv_import_preview again.");
        if (plan is null) throw new ModelContextProtocol.McpException("Preview expired.");
        FinanceToolSupport.Check(Interlocked.CompareExchange(ref plan.Consumed, 1, 0) == 0, "This preview token has already been consumed. Inspect transactions before retrying.");
        cache.Remove(Key(user, previewToken));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, plan.Mapping.AccountId, user, cancellationToken);
        var preview = await FinanceToolSupport.Run(() => importer.PreviewImportAsync(user, plan.Content, Map(plan.Mapping), cancellationToken));
        FinanceToolSupport.Check(preview.All(r => !r.HasErrors), "CSV contains invalid rows. Correct the data and create a fresh preview.");
        FinanceToolSupport.Check(Fingerprint(await PreviewRows(db, user, preview, cancellationToken)) == plan.PreviewFingerprint,
            "CSV category resolution changed. Create a fresh preview before importing.");
        var result = await FinanceToolSupport.Run(() => importer.ImportTransactionsAsync(user, plan.Content, Map(plan.Mapping), cancellationToken));
        return new(result.ImportedCount, result.SkippedCount, result.TotalRows,
            result.Errors.Count == 0 ? [] : ["Some rows could not be imported. Review the imported transactions before retrying; no raw database errors are exposed."]);
    }

    /// <summary>Exports one SQL-paged transaction selection in an import-compatible column layout.</summary>
    [McpServerTool(Name = "csv_transactions_export", ReadOnly = true, OpenWorld = false)]
    [Description("Export a filtered transaction page (maximum 100) to CSV. ISO yyyy-MM-dd dates, invariant decimal amounts; columns Date,Amount,Payee,Category,Memo,ReferenceNumber,Cleared,Status. Text formula prefixes are escaped for spreadsheet safety. HasMore/NextOffset support continuation.")]
    public async Task<CsvExportToolDto> Export(TransactionSearchInput input, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = await TransactionsTools.Query(db, user, input, cancellationToken);
        var sorted = input.OldestFirst ? query.OrderBy(t => t.Date).ThenBy(t => t.Id) : query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        var rows = await sorted.Skip(input.Offset).Take(input.Limit + 1).Select(t => new
        {
            t.Date, t.Amount, Payee = t.Payee != null && t.Payee.UserId == user ? t.Payee.Name : null,
            Category = t.Category != null && t.Category.UserId == user ? t.Category.Name : null,
            t.Memo, t.ReferenceNumber, t.IsCleared, t.Status
        }).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Date,Amount,Payee,Category,Memo,ReferenceNumber,Cleared,Status\r\n");
        foreach (var row in rows.Take(input.Limit))
            csv.AppendLine(string.Join(',', row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Amount.ToString(CultureInfo.InvariantCulture), Cell(row.Payee), Cell(row.Category), Cell(row.Memo),
                Cell(row.ReferenceNumber), row.IsCleared ? "true" : "false", row.Status.ToString()));
        return new(csv.ToString(), Math.Min(rows.Count, input.Limit), rows.Count > input.Limit, input.Offset + input.Limit);
    }

    private static string Key(string user, string token) => $"mcp:csv:{user}:{token}";
    private static string Fingerprint(IReadOnlyList<CsvPreviewRowToolDto> rows)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));

    private static async Task<List<CsvPreviewRowToolDto>> PreviewRows(ApplicationDbContext db, string user, List<TransactionCsvPreviewRow> rows, CancellationToken ct)
    {
        var names = rows.Select(r => r.Category).OfType<string>().Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.ToUpperInvariant()).Distinct().ToList();
        var categories = await db.Categories.AsNoTracking().Where(c => c.UserId == user && c.IsActive && names.Contains(c.Name.ToUpper()))
            .Select(c => new { c.Id, c.Name, c.CategoryGroup.Type, GroupUser = c.CategoryGroup.UserId })
            .Take(101).ToListAsync(ct);
        FinanceToolSupport.Check(categories.Count <= 100 && categories.All(c => c.GroupUser == user), "CSV category resolution is ambiguous or invalid.");
        var lookup = categories.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var output = new List<CsvPreviewRowToolDto>(rows.Count);
        foreach (var r in rows)
        {
            var amount = r.Amount;
            if (amount.HasValue) FinanceToolSupport.Money(amount.Value);
            if (r.Date.HasValue) FinanceToolSupport.Date(r.Date.Value);
            int? categoryId = null;
            if (r.Category is not null && lookup.TryGetValue(r.Category, out var matches))
            {
                FinanceToolSupport.Check(matches.Count == 1, "Multiple active categories share a CSV category name. Rename duplicates before import.");
                var match = matches[0];
                categoryId = match.Id;
                if (amount.HasValue) amount = match.Type == CategoryType.Income ? Math.Abs(amount.Value) : -Math.Abs(amount.Value);
            }
            output.Add(new(r.RowNumber, r.Date, amount, r.Payee, r.Category, categoryId, r.Memo, r.ReferenceNumber,
                r.IsCleared, r.HasErrors ? ["Row contains an invalid date, amount or column mapping."] : []));
        }
        return output;
    }

    private static string Cell(string? text)
    {
        text ??= "";
        var trimmed = text.TrimStart();
        if (trimmed.Length > 0 && (trimmed[0] is '=' or '+' or '-' or '@' || text[0] is '\t' or '\r' or '\n')) text = "'" + text;
        // The application parser is line-based, so exports use a single physical line per transaction.
        return "\"" + text.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"") + "\"";
    }

    private static void ValidateMapping(CsvMappingToolInput m)
    {
        FinanceToolSupport.Check(Enum.IsDefined(m.DefaultStatus), "Invalid default status.");
        FinanceToolSupport.Check(m.DateFormat is "yyyy-MM-dd" or "MM/dd/yyyy" or "dd/MM/yyyy" or "dd.MM.yyyy", "Unsupported CSV date format.");
        var columns = new int?[] { m.DateColumn, m.AmountColumn, m.PayeeColumn, m.CategoryColumn, m.MemoColumn, m.ReferenceNumberColumn, m.ClearedColumn };
        FinanceToolSupport.Check(columns.All(c => c is null or >= 0 and < 100), "Column indices must be between 0 and 99.");
        FinanceToolSupport.Check(columns.Where(c => c.HasValue).Distinct().Count() == columns.Count(c => c.HasValue), "Each mapped column must be unique.");
    }

    private static TransactionCsvColumnMapping Map(CsvMappingToolInput m) => new()
    {
        AccountId = m.AccountId, DateColumn = m.DateColumn, AmountColumn = m.AmountColumn, PayeeColumn = m.PayeeColumn,
        CategoryColumn = m.CategoryColumn, MemoColumn = m.MemoColumn, ReferenceNumberColumn = m.ReferenceNumberColumn,
        ClearedColumn = m.ClearedColumn, HasHeaderRow = m.HasHeaderRow, DateFormat = m.DateFormat,
        InvertAmounts = m.InvertAmounts, DefaultStatus = m.DefaultStatus
    };

    private sealed class ImportPlan(string content, CsvMappingToolInput mapping, string previewFingerprint)
    {
        internal string Content { get; } = content;
        internal CsvMappingToolInput Mapping { get; } = mapping;
        internal string PreviewFingerprint { get; } = previewFingerprint;
        internal int Consumed;
    }
}

/// <summary>Explicit zero-based CSV mapping, not arbitrary service state.</summary>
public sealed record CsvMappingToolInput(int AccountId, int DateColumn, int AmountColumn, int? PayeeColumn = null,
    int? CategoryColumn = null, int? MemoColumn = null, int? ReferenceNumberColumn = null, int? ClearedColumn = null,
    bool HasHeaderRow = true, string DateFormat = "yyyy-MM-dd", bool InvertAmounts = false, TransactionStatus DefaultStatus = TransactionStatus.Posted);
/// <summary>Safe parsed preview row without raw parser/database exception data.</summary>
public sealed record CsvPreviewRowToolDto(int RowNumber, DateTime? Date, decimal? Amount, string? Payee,
    string? Category, int? ResolvedCategoryId, string? Memo, string? ReferenceNumber, bool? IsCleared, IReadOnlyList<string> Errors);
/// <summary>Preview token and parsed rows. No financial records have been written.</summary>
public sealed record CsvPreviewToolDto(string PreviewToken, DateTimeOffset ExpiresAt, IReadOnlyList<CsvPreviewRowToolDto> Rows, int InvalidRows);
/// <summary>Import counts with sanitized errors.</summary>
public sealed record CsvImportToolResult(int ImportedCount, int SkippedCount, int TotalRows, IReadOnlyList<string> Errors);
/// <summary>Bounded UTF-8 CSV content and continuation metadata.</summary>
public sealed record CsvExportToolDto(string Content, int RowCount, bool HasMore, int NextOffset);

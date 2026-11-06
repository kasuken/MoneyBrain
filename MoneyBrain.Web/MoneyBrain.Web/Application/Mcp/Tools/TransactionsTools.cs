using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Transactions;
using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.FlagManagement;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Transactions.StatusManagement;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Bounded transaction reads and owner-validated application mutations.</summary>
[McpServerToolType]
public sealed class TransactionsTools(McpAccess access, ITransactionService transactions, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Searches with filtering and paging performed in SQL.</summary>
    [McpServerTool(Name = "transactions_search", ReadOnly = true, OpenWorld = false)]
    [Description("Search transactions with validated dates, account/category/payee, amount, status, cleared/reconciled, recurrence and text filters. Signed decimal amounts; ISO dates; date/id sort; maximum 100 per page.")]
    public async Task<FinancePage<FinanceTransactionDto>> Search(TransactionSearchInput input, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = await Query(db, user, input, cancellationToken);
        var sorted = input.OldestFirst ? query.OrderBy(t => t.Date).ThenBy(t => t.Id) : query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        var rows = await FinanceProjections.Transactions(sorted.Skip(input.Offset).Take(input.Limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(input.Limit).ToList(), input.Offset, rows.Count > input.Limit);
    }

    /// <summary>Reads one transaction and a bounded split list.</summary>
    [McpServerTool(Name = "transactions_get", ReadOnly = true, OpenWorld = false)]
    [Description("Get an owned transaction. Split lines are separately paged with transactions_splits_list to keep responses bounded.")]
    public async Task<FinanceTransactionDto> Get(int transactionId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => t.Id == transactionId && t.UserId == user && t.Account.UserId == user))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ModelContextProtocol.McpException("Transaction not found.");
    }

    /// <summary>Reads split lines without navigation graphs.</summary>
    [McpServerTool(Name = "transactions_splits_list", ReadOnly = true, OpenWorld = false)]
    [Description("List split lines of an owned transaction. Maximum 100 per page.")]
    public async Task<FinancePage<SplitToolDto>> Splits(int transactionId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        FinanceToolSupport.Check(await db.Transactions.AnyAsync(t => t.Id == transactionId && t.UserId == user, cancellationToken), "Transaction not found.");
        var rows = await db.TransactionSplits.AsNoTracking().Where(s => s.TransactionId == transactionId && s.Transaction.UserId == user)
            .OrderBy(s => s.Id).Skip(offset).Take(limit + 1).Select(s => new SplitToolDto(s.Id, s.CategoryId, s.Amount, s.Memo)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Creates a regular or recurring transaction through the application service.</summary>
    [McpServerTool(Name = "transactions_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a transaction or recurring template. Category type normalizes amount sign. Not retry-safe: search after timeout before retrying.")]
    public async Task<FinanceTransactionDto> Create(TransactionWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Validate(db, user, input, cancellationToken);
        return FinanceProjections.Transaction(await FinanceToolSupport.Run(() => transactions.CreateTransactionAsync(
            user, input.AccountId, input.Date, input.Amount, input.PayeeId, input.CategoryId, input.Memo, input.Status,
            input.IsCleared, input.ReferenceNumber, input.Tags, input.IsRecurring, input.RecurrenceFrequency, input.RecurrenceStartDate, cancellationToken)));
    }

    /// <summary>Replaces editable fields of an ordinary owned transaction.</summary>
    [McpServerTool(Name = "transactions_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Replace ordinary transaction fields after transactions_get. Cannot move accounts, edit transfers/reconciled records, or silently discard splits. Use transactions_splits_update for split records.")]
    public async Task<MutationResult> Update(int transactionId, TransactionWriteInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var current = await FinanceToolSupport.MutableTransaction(db, transactionId, user, cancellationToken);
        await Validate(db, user, input, cancellationToken);
        FinanceToolSupport.Check(current.AccountId == input.AccountId, "Moving transactions between accounts is not supported.");
        FinanceToolSupport.Check(!await db.TransactionSplits.AnyAsync(s => s.TransactionId == transactionId, cancellationToken), "Use transactions_splits_update for a split transaction.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => transactions.UpdateTransactionAsync(transactionId, user, input.Date, input.Amount,
            input.PayeeId, input.CategoryId, input.Memo, input.Status, input.IsCleared, input.ReferenceNumber, input.Tags,
            input.IsRecurring, input.RecurrenceFrequency, input.RecurrenceStartDate, cancellationToken)));
    }

    /// <summary>Deletes an ordinary transaction while preserving transfer and reconciliation invariants.</summary>
    [McpServerTool(Name = "transactions_delete", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Delete an ordinary transaction after transactions_get. Reconciled records and transfer sides are rejected. Use transfers_delete for both transfer sides.")]
    public async Task<MutationResult> Delete(int transactionId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.MutableTransaction(db, transactionId, user, cancellationToken);
        FinanceToolSupport.Check(!await db.Transactions.AnyAsync(t => t.RecurringTemplateId == transactionId, cancellationToken), "A recurring template with generated occurrences cannot be deleted.");
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => transactions.DeleteTransactionAsync(transactionId, user, cancellationToken)));
    }

    /// <summary>Creates one transaction with up to 100 explicit splits.</summary>
    [McpServerTool(Name = "transactions_splits_create", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a split transaction. Each split amount is sign-normalized by its category type (Income positive, Expense negative; mixed lines such as cashback allowed); normalized amounts must sum exactly to the total. Top-level category and recurrence are unsupported. Not retry-safe.")]
    public async Task<FinanceTransactionDto> CreateSplits(TransactionWriteInput input, List<SplitToolInput> splits, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await Validate(db, user, input, cancellationToken);
        var lines = await ValidateSplits(db, user, input, splits, cancellationToken);
        return FinanceProjections.Transaction(await FinanceToolSupport.Run(() => transactions.CreateTransactionWithSplitsAsync(
            user, input.AccountId, input.Date, input.Amount, input.PayeeId, input.Memo, input.Status, input.IsCleared,
            input.ReferenceNumber, input.Tags, lines, cancellationToken)));
    }

    /// <summary>Replaces all split lines, validating category ownership.</summary>
    [McpServerTool(Name = "transactions_splits_update", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Replace all split lines and transaction fields after transactions_get and transactions_splits_list. Category-normalized split amounts must sum exactly to the total; transfers and reconciled records rejected.")]
    public async Task<MutationResult> UpdateSplits(int transactionId, TransactionWriteInput input, List<SplitToolInput> splits, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var current = await FinanceToolSupport.MutableTransaction(db, transactionId, user, cancellationToken);
        FinanceToolSupport.Check(current.AccountId == input.AccountId && !current.IsRecurring, "Cannot move accounts or replace a recurring template with splits.");
        await Validate(db, user, input, cancellationToken);
        var lines = await ValidateSplits(db, user, input, splits, cancellationToken);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => transactions.UpdateTransactionWithSplitsAsync(transactionId, user, input.Date,
            input.Amount, input.PayeeId, input.Memo, input.Status, input.IsCleared, input.ReferenceNumber, input.Tags, lines, cancellationToken)));
    }

    /// <summary>Previews a bounded explicit transaction selection for bulk edits.</summary>
    [McpServerTool(Name = "transactions_bulk_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview at most 100 explicit owned transaction IDs before bulk changes. Includes locked and transfer flags; bulk writes reject them instead of bypassing protection.")]
    public async Task<IReadOnlyList<FinanceTransactionDto>> BulkPreview(List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var ids = FinanceToolSupport.Ids(transactionIds);
        FinanceToolSupport.Check(await db.Transactions.CountAsync(t => ids.Contains(t.Id) && t.UserId == user && t.Account.UserId == user, cancellationToken) == ids.Count, "Transaction not found.");
        return await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => ids.Contains(t.Id) && t.UserId == user).OrderBy(t => t.Id)).ToListAsync(cancellationToken);
    }

    /// <summary>Applies reference/tag changes to an explicit owned selection.</summary>
    [McpServerTool(Name = "transactions_bulk_update", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("After transactions_bulk_preview, set/clear category, payee or tags on up to 100 ordinary transactions. Reconciled records and transfers are rejected. Does not expose a reconciled-flag override.")]
    public async Task<BulkMutationToolDto> BulkUpdate(BulkTransactionToolInput input, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Validate(input);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var ids = await MutableSelection(db, user, input.TransactionIds, cancellationToken);
        await FinanceToolSupport.Category(db, input.CategoryId, user, cancellationToken);
        await FinanceToolSupport.Payee(db, input.PayeeId, user, cancellationToken);
        FinanceToolSupport.Check(!(input.ClearCategory && input.CategoryId.HasValue) && !(input.ClearPayee && input.PayeeId.HasValue), "Cannot both clear and set a field.");
        FinanceToolSupport.Check(input.CategoryId.HasValue || input.PayeeId.HasValue || input.Tags is not null || input.ClearCategory || input.ClearPayee, "Select a field to update.");
        FinanceToolSupport.OptionalText(input.Tags, "Tags", 500);
        if (input.CategoryId.HasValue || input.ClearCategory)
            FinanceToolSupport.Check(!await db.TransactionSplits.AnyAsync(s => ids.Contains(s.TransactionId), cancellationToken), "Use split tools to change split categories.");
        if (input.CategoryId is int categoryId)
        {
            var type = await db.Categories.Where(c => c.Id == categoryId && c.UserId == user).Select(c => c.CategoryGroup.Type).SingleAsync(cancellationToken);
            FinanceToolSupport.Check(!await db.Transactions.AnyAsync(t => ids.Contains(t.Id) && t.UserId == user &&
                (type == CategoryType.Income ? t.Amount < 0 : t.Amount > 0), cancellationToken),
                "Bulk category change would conflict with transaction signs. Update individual transaction amounts explicitly.");
        }
        var r = await FinanceToolSupport.Run(() => transactions.BulkUpdateTransactionsAsync(user, new BulkEditTransactionRequest
        {
            TransactionIds = ids, CategoryId = input.CategoryId, PayeeId = input.PayeeId,
            Tags = input.Tags?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            ClearCategory = input.ClearCategory, ClearPayee = input.ClearPayee
        }, cancellationToken));
        return new(r.UpdatedCount, r.SkippedTransactionIds);
    }

    /// <summary>Sets status on an explicit protected selection.</summary>
    [McpServerTool(Name = "transactions_status_set", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("After transactions_bulk_preview, set Pending or Posted on at most 100 ordinary transactions. Only posted activity affects budgets. Reconciled records/transfers rejected.")]
    public async Task<BulkMutationToolDto> SetStatus(List<int> transactionIds, TransactionStatus status, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Check(Enum.IsDefined(status), "Invalid transaction status.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var ids = await MutableSelection(db, user, transactionIds, cancellationToken);
        var r = await FinanceToolSupport.Run(() => transactions.BulkUpdateStatusAsync(user, new StatusUpdateRequest { TransactionIds = ids, NewStatus = status, SkipReconciled = true, SkipTransfers = true }, cancellationToken));
        return new(r.UpdatedCount, r.SkippedTransactionIds);
    }

    /// <summary>Sets, rather than toggles, the cleared flag for retry safety.</summary>
    [McpServerTool(Name = "transactions_cleared_set", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("After transactions_bulk_preview, set the cleared flag on up to 100 ordinary transactions. Reconciled records/transfers rejected. Absolute set is retry-safe, unlike toggle.")]
    public async Task<BulkMutationToolDto> SetCleared(List<int> transactionIds, bool isCleared, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var ids = await MutableSelection(db, user, transactionIds, cancellationToken);
        var r = await FinanceToolSupport.Run(() => transactions.BulkUpdateClearedFlagAsync(user, new ClearedFlagRequest { TransactionIds = ids, IsCleared = isCleared, SkipReconciled = true }, cancellationToken));
        return new(r.UpdatedCount, r.SkippedTransactionIds);
    }

    internal static async Task<IQueryable<Transaction>> Query(ApplicationDbContext db, string user, TransactionSearchInput input, CancellationToken ct)
    {
        FinanceToolSupport.Validate(input);
        FinanceToolSupport.Page(input.Offset, input.Limit);
        FinanceToolSupport.Check(input.StartDate is null || input.EndDate is null || input.StartDate <= input.EndDate, "Start date must not follow end date.");
        FinanceToolSupport.Check(input.MinAmount is null || input.MaxAmount is null || input.MinAmount <= input.MaxAmount, "Minimum amount must not exceed maximum.");
        FinanceToolSupport.Check(input.Status is null || Enum.IsDefined(input.Status.Value), "Invalid status.");
        if (input.AccountId is int account) await FinanceToolSupport.Account(db, account, user, ct);
        await FinanceToolSupport.Category(db, input.CategoryId, user, ct);
        await FinanceToolSupport.Payee(db, input.PayeeId, user, ct);
        var query = db.Transactions.AsNoTracking().Where(t => t.UserId == user && t.Account.UserId == user);
        if (input.AccountId is int a) query = query.Where(t => t.AccountId == a);
        if (input.CategoryId is int c) query = query.Where(t => t.CategoryId == c || t.Splits.Any(s => s.CategoryId == c));
        if (input.PayeeId is int p) query = query.Where(t => t.PayeeId == p);
        if (input.StartDate is DateTime start) query = query.Where(t => t.Date >= start);
        if (input.EndDate is DateTime end) query = query.Where(t => t.Date <= end);
        if (input.MinAmount is decimal min) query = query.Where(t => t.Amount >= min);
        if (input.MaxAmount is decimal max) query = query.Where(t => t.Amount <= max);
        if (input.Status is TransactionStatus status) query = query.Where(t => t.Status == status);
        if (input.IsCleared is bool cleared) query = query.Where(t => t.IsCleared == cleared);
        if (input.IsReconciled is bool reconciled) query = query.Where(t => t.IsReconciled == reconciled);
        if (input.IsRecurring is bool recurring) query = query.Where(t => t.IsRecurring == recurring);
        if (!input.IncludeTransfers) query = query.Where(t => t.TransferTransactionId == null);
        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            var text = input.SearchText.ToLowerInvariant();
            query = query.Where(t => t.Memo != null && t.Memo.ToLower().Contains(text) ||
                t.ReferenceNumber != null && t.ReferenceNumber.ToLower().Contains(text) ||
                t.Payee != null && t.Payee.UserId == user && t.Payee.Name.ToLower().Contains(text) ||
                t.Category != null && t.Category.UserId == user && t.Category.Name.ToLower().Contains(text));
        }
        return query;
    }

    private static async Task Validate(ApplicationDbContext db, string user, TransactionWriteInput input, CancellationToken ct)
    {
        FinanceToolSupport.Validate(input);
        FinanceToolSupport.Check(Enum.IsDefined(input.Status), "Invalid transaction status.");
        FinanceToolSupport.Money(input.Amount);
        FinanceToolSupport.Check(input.Date.Year >= 1900 && input.Date.Year < 9999, "Date must be between years 1900 and 9998.");
        await FinanceToolSupport.Account(db, input.AccountId, user, ct);
        await FinanceToolSupport.Payee(db, input.PayeeId, user, ct);
        await FinanceToolSupport.Category(db, input.CategoryId, user, ct);
        if (input.IsRecurring)
            FinanceToolSupport.Check(input.RecurrenceFrequency is { } frequency && Enum.IsDefined(frequency) && input.RecurrenceStartDate is { Year: >= 1900 and < 9999 }, "Recurring transactions require a valid frequency and ISO start date.");
        else FinanceToolSupport.Check(input.RecurrenceFrequency is null && input.RecurrenceStartDate is null, "Non-recurring transactions must omit recurrence settings.");
    }

    private static async Task<List<TransactionSplitDto>> ValidateSplits(ApplicationDbContext db, string user, TransactionWriteInput input, List<SplitToolInput> splits, CancellationToken ct)
    {
        FinanceToolSupport.Check(splits is { Count: >= 2 and <= 100 }, "Provide between 2 and 100 split lines.");
        FinanceToolSupport.Check(input.CategoryId is null && !input.IsRecurring, "Split operations must omit top-level category and recurrence.");
        FinanceToolSupport.Check(input.Amount != 0, "Transaction amount cannot be zero.");
        var lines = new List<TransactionSplitDto>(splits.Count);
        var normalizedTotal = 0m;
        foreach (var s in splits)
        {
            FinanceToolSupport.Validate(s);
            FinanceToolSupport.Money(s.Amount);
            FinanceToolSupport.Check(s.Amount != 0, "Split amounts must be nonzero.");
            await FinanceToolSupport.Category(db, s.CategoryId, user, ct);
            // Mirrors the service's category sign normalization so the exact-total check uses stored amounts.
            var normalized = s.Amount;
            if (s.CategoryId is int categoryId)
            {
                var type = await db.Categories.Where(c => c.Id == categoryId && c.UserId == user).Select(c => c.CategoryGroup.Type).SingleAsync(ct);
                normalized = type == CategoryType.Income ? Math.Abs(s.Amount) : -Math.Abs(s.Amount);
            }
            normalizedTotal += normalized;
            lines.Add(new TransactionSplitDto { CategoryId = s.CategoryId, Amount = s.Amount, Memo = s.Memo });
        }
        FinanceToolSupport.Check(normalizedTotal == input.Amount, "Category-normalized split amounts must equal the transaction amount exactly.");
        return lines;
    }

    private static async Task<List<int>> MutableSelection(ApplicationDbContext db, string user, List<int> selection, CancellationToken ct)
    {
        var ids = FinanceToolSupport.Ids(selection);
        var rows = await db.Transactions.Where(t => ids.Contains(t.Id) && t.UserId == user && t.Account.UserId == user)
            .Select(t => new { t.Id, t.IsReconciled, t.TransferTransactionId }).ToListAsync(ct);
        FinanceToolSupport.Check(rows.Count == ids.Count, "Transaction not found.");
        FinanceToolSupport.Check(rows.All(t => !t.IsReconciled && t.TransferTransactionId is null), "Selection contains a reconciled transaction or a transfer. Refresh the preview.");
        return ids;
    }
}

/// <summary>Bounded SQL search input; start/end dates are inclusive ISO timestamps.</summary>
public sealed record TransactionSearchInput(int? AccountId = null, int? CategoryId = null, int? PayeeId = null,
    DateTime? StartDate = null, DateTime? EndDate = null, decimal? MinAmount = null, decimal? MaxAmount = null,
    TransactionStatus? Status = null, bool? IsCleared = null, bool? IsReconciled = null, bool? IsRecurring = null,
    bool IncludeTransfers = true, [property: StringLength(200)] string? SearchText = null,
    bool OldestFirst = false, int Offset = 0, int Limit = 100);
/// <summary>Explicit transaction fields; account ID is immutable on update.</summary>
public sealed record TransactionWriteInput(int AccountId, DateTime Date, decimal Amount, int? PayeeId = null, int? CategoryId = null,
    [property: StringLength(1000)] string? Memo = null, TransactionStatus Status = TransactionStatus.Pending,
    bool IsCleared = false, [property: StringLength(100)] string? ReferenceNumber = null,
    [property: StringLength(500)] string? Tags = null, bool IsRecurring = false,
    RecurrenceFrequency? RecurrenceFrequency = null, DateTime? RecurrenceStartDate = null);
/// <summary>Split input; existing split IDs are never accepted as writable foreign keys.</summary>
public sealed record SplitToolInput(decimal Amount, int? CategoryId = null, [property: StringLength(1000)] string? Memo = null);
/// <summary>Flat split output.</summary>
public sealed record SplitToolDto(int Id, int? CategoryId, decimal Amount, string? Memo);
/// <summary>Safe bounded bulk metadata update without reconciliation override.</summary>
public sealed record BulkTransactionToolInput(List<int> TransactionIds, int? CategoryId = null, int? PayeeId = null,
    [property: StringLength(500)] string? Tags = null, bool ClearCategory = false, bool ClearPayee = false);
/// <summary>Mutation counts and IDs skipped by the application, without exception text.</summary>
public sealed record BulkMutationToolDto(int UpdatedCount, IReadOnlyList<int> SkippedTransactionIds);

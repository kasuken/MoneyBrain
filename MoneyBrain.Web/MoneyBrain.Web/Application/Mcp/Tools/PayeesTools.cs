using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Transactions;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Owner-scoped payee lookup, creation, renaming and safe merge operations.</summary>
[McpServerToolType]
public sealed class PayeesTools(McpAccess access, IPayeeService payees, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists payees with bounded SQL paging.</summary>
    [McpServerTool(Name = "payees_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned payees, optionally matching a name. Maximum 100 per page.")]
    public async Task<FinancePage<PayeeToolDto>> List(string? search = null, bool includeInactive = false, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        FinanceToolSupport.OptionalText(search, "Search", 100);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Payees.AsNoTracking().Where(p => p.UserId == user && (includeInactive || p.IsActive) &&
                (search == null || p.Name.Contains(search)))
            .OrderBy(p => p.Name).ThenBy(p => p.Id).Skip(offset).Take(limit + 1)
            .Select(p => new PayeeToolDto(p.Id, p.Name, p.DefaultCategoryId, p.IsActive)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Finds or creates a normalized payee.</summary>
    [McpServerTool(Name = "payees_create_or_get", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Find or create a payee by normalized name. Optional default category must belong to you. Sequential calls reuse matches, but concurrent retries may create duplicates; inspect payees_list after timeout.")]
    public async Task<PayeeToolDto> Create(string name, int? defaultCategoryId = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name", 200);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Category(db, defaultCategoryId, user, cancellationToken);
        var p = await FinanceToolSupport.Run(() => payees.CreateOrGetPayeeAsync(user, name, defaultCategoryId, cancellationToken));
        return new(p.Id, p.Name, p.DefaultCategoryId, p.IsActive);
    }

    /// <summary>Renames a payee without changing IDs.</summary>
    [McpServerTool(Name = "payees_rename", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Rename an owned payee using application normalization. Preview payees_list first.")]
    public async Task<MutationResult> Rename(int payeeId, string name, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        name = FinanceToolSupport.Text(name, "Name", 200);
        return FinanceToolSupport.Changed(await FinanceToolSupport.Run(() => payees.RenamePayeeAsync(payeeId, user, name, cancellationToken)));
    }

    /// <summary>Previews a payee merge without loading transaction history.</summary>
    [McpServerTool(Name = "payees_merge_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Count unlocked and reconciled transactions for up to 100 source payees. Reconciled references remain unchanged during merge.")]
    public async Task<PayeeMergePreviewToolDto> MergePreview(int targetPayeeId, List<int> sourcePayeeIds, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Preview(db, user, targetPayeeId, sourcePayeeIds, cancellationToken);
    }

    /// <summary>Merges selected payees, leaving reconciled transactions untouched.</summary>
    [McpServerTool(Name = "payees_merge", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("After payees_merge_preview, repoint unlocked source transactions and archive source payees. Reconciled transactions stay unchanged.")]
    public async Task<PayeeMergePreviewToolDto> Merge(int targetPayeeId, List<int> sourcePayeeIds, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var preview = await Preview(db, user, targetPayeeId, sourcePayeeIds, cancellationToken);
        FinanceToolSupport.Check(await FinanceToolSupport.Run(() => payees.MergePayeesAsync(user, targetPayeeId, sourcePayeeIds, cancellationToken)), "Payee merge could not be completed.");
        return preview;
    }

    /// <summary>Previews the supported unused-payee cleanup operation.</summary>
    [McpServerTool(Name = "payees_unused_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview all active payees with no transaction references before cleanup. Rejects more than 100 candidates rather than returning a truncated destructive preview.")]
    public async Task<IReadOnlyList<PayeeToolDto>> UnusedPreview(CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Unused(db, user, cancellationToken);
    }

    /// <summary>Archives only when the unused-payee candidate set still matches the explicit preview.</summary>
    [McpServerTool(Name = "payees_unused_archive", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("After payees_unused_preview, supply its exact payee IDs to archive unused payees. Fails if the candidate set changed. No used payee is hard-deleted.")]
    public async Task<UnusedPayeeCleanupToolDto> ArchiveUnused(List<int> expectedPayeeIds, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        var expected = FinanceToolSupport.Ids(expectedPayeeIds);
        return new(await FinanceToolSupport.Run(() => payees.DeleteUnusedPayeesAsync(user, expected, cancellationToken)));
    }

    private static async Task<IReadOnlyList<PayeeToolDto>> Unused(ApplicationDbContext db, string user, CancellationToken ct)
    {
        var rows = await db.Payees.AsNoTracking().Where(p => p.UserId == user && p.IsActive && !db.Transactions.Any(t => t.PayeeId == p.Id))
            .OrderBy(p => p.Id).Take(101).Select(p => new PayeeToolDto(p.Id, p.Name, p.DefaultCategoryId, p.IsActive)).ToListAsync(ct);
        FinanceToolSupport.Check(rows.Count <= 100, "More than 100 unused payees exist. Use the application to clean up this larger selection.");
        return rows;
    }

    private static async Task<PayeeMergePreviewToolDto> Preview(ApplicationDbContext db, string user, int target, List<int> sources, CancellationToken ct)
    {
        var ids = FinanceToolSupport.Ids(sources);
        FinanceToolSupport.Check(!ids.Contains(target), "Target must not be a source.");
        await FinanceToolSupport.Payee(db, target, user, ct);
        FinanceToolSupport.Check(await db.Payees.CountAsync(p => p.UserId == user && ids.Contains(p.Id), ct) == ids.Count, "Source payee not found.");
        var query = db.Transactions.Where(t => t.UserId == user && t.PayeeId.HasValue && ids.Contains(t.PayeeId.Value));
        return new(target, ids, await query.CountAsync(t => !t.IsReconciled, ct), await query.CountAsync(t => t.IsReconciled, ct));
    }
}

/// <summary>Safe payee metadata.</summary>
public sealed record PayeeToolDto(int Id, string Name, int? DefaultCategoryId, bool IsActive);
/// <summary>Summary of a payee merge.</summary>
public sealed record PayeeMergePreviewToolDto(int TargetPayeeId, IReadOnlyList<int> SourcePayeeIds, int MutableTransactions, int ReconciledTransactionsSkipped);
/// <summary>Number of unused payees archived by the application.</summary>
public sealed record UnusedPayeeCleanupToolDto(int ArchivedCount);

using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Reconciliation;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Statement reconciliation without exposing unlock/undo operations.</summary>
[McpServerToolType]
public sealed class ReconciliationTools(McpAccess access, IReconciliationService reconciliations, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists owner-scoped statement sessions.</summary>
    [McpServerTool(Name = "reconciliation_list", ReadOnly = true, OpenWorld = false)]
    [Description("List owned reconciliation sessions, optional account filter. Maximum 100 per page.")]
    public async Task<FinancePage<ReconciliationToolDto>> List(int? accountId = null, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (accountId is int account) await FinanceToolSupport.Account(db, account, user, cancellationToken);
        var rows = await Project(db.Reconciliations.AsNoTracking().Where(r => r.UserId == user && r.Account.UserId == user && (accountId == null || r.AccountId == accountId))
            .OrderByDescending(r => r.Id).Skip(offset).Take(limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Gets the statement session and current stored reconciliation totals.</summary>
    [McpServerTool(Name = "reconciliation_get", ReadOnly = true, OpenWorld = false)]
    [Description("Get an owned statement session, balances and difference. Use reconciliation_candidates to select posted entries and reconciliation_selection_preview before locking them.")]
    public async Task<ReconciliationToolDto> Get(int reconciliationId, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return Map(await Session(db, reconciliationId, user, cancellationToken));
    }

    /// <summary>Starts a reconciliation for an owned account.</summary>
    [McpServerTool(Name = "reconciliation_start", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Start a statement reconciliation for an owned account with ISO statement date and decimal closing balance. Not retry-safe: inspect reconciliation_list after timeout.")]
    public async Task<ReconciliationToolDto> Start(int accountId, DateTime statementDate, decimal statementBalance, string? notes = null, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        FinanceToolSupport.Money(statementBalance);
        FinanceToolSupport.OptionalText(notes, "Notes", 1000);
        FinanceToolSupport.Check(statementDate.Year is >= 1900 and < 9999, "Invalid statement date.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await FinanceToolSupport.Account(db, accountId, user, cancellationToken);
        return Map(await FinanceToolSupport.Run(() => reconciliations.StartReconciliationAsync(accountId, user, statementDate, statementBalance, notes, cancellationToken)));
    }

    /// <summary>Lists eligible posted entries in bounded pages.</summary>
    [McpServerTool(Name = "reconciliation_candidates", ReadOnly = true, OpenWorld = false)]
    [Description("List posted, unreconciled transactions in this account through its statement date. Maximum 100 per page; no all-history loading.")]
    public async Task<FinancePage<FinanceTransactionDto>> Candidates(int reconciliationId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var session = await Session(db, reconciliationId, user, cancellationToken);
        var rows = await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => t.UserId == user && t.AccountId == session.AccountId &&
                !t.IsReconciled && t.Status == TransactionStatus.Posted && t.Date <= session.StatementDate)
            .OrderBy(t => t.Date).ThenBy(t => t.Id).Skip(offset).Take(limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Previews the exact eligible records that would become immutable.</summary>
    [McpServerTool(Name = "reconciliation_selection_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview up to 100 explicit eligible transaction IDs before permanently locking them into a reconciliation. Returns IDs and signed total.")]
    public async Task<ReconciliationSelectionToolDto> SelectionPreview(int reconciliationId, List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Selection(db, user, reconciliationId, transactionIds, cancellationToken);
    }

    /// <summary>Locks selected eligible records into an active session.</summary>
    [McpServerTool(Name = "reconciliation_select", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("After reconciliation_selection_preview, lock up to 100 posted transactions through the statement date. No undo/unreconcile tool is exposed. Verify state after a timeout.")]
    public async Task<ReconciliationSelectionToolDto> Select(int reconciliationId, List<int> transactionIds, decimal expectedTotal, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var summary = await Selection(db, user, reconciliationId, transactionIds, cancellationToken);
        FinanceToolSupport.Check(summary.Total == expectedTotal, "Selection changed. Refresh reconciliation_selection_preview.");
        FinanceToolSupport.Check(await FinanceToolSupport.Run(() => reconciliations.ReconcileTransactionsAsync(reconciliationId, user, transactionIds, cancellationToken)),
            "Reconciliation selection could not be completed.");
        return summary;
    }

    /// <summary>Finalizes only a balanced active session.</summary>
    [McpServerTool(Name = "reconciliation_finalize", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Finalize an active session only when its live reconciled balance exactly matches the statement. Preview reconciliation_get first. Completed sessions cannot be unlocked/deleted through MCP.")]
    public async Task<ReconciliationToolDto> Finalize(int reconciliationId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var session = await Session(db, reconciliationId, user, cancellationToken);
        if (session.IsCompleted) return Map(session);
        var balance = await FinanceToolSupport.Run(() => reconciliations.CalculateReconciledBalanceAsync(reconciliationId, user, cancellationToken));
        FinanceToolSupport.Check(balance == session.StatementBalance, "Reconciliation difference must be exactly zero before finalizing.");
        return Map(await FinanceToolSupport.Run(() => reconciliations.CompleteReconciliationAsync(reconciliationId, user, cancellationToken)));
    }

    private static async Task<ReconciliationSelectionToolDto> Selection(ApplicationDbContext db, string user, int id, List<int> selection, CancellationToken ct)
    {
        var ids = FinanceToolSupport.Ids(selection);
        var session = await Session(db, id, user, ct);
        FinanceToolSupport.Check(!session.IsCompleted, "Reconciliation is completed.");
        var rows = await db.Transactions.AsNoTracking().Where(t => ids.Contains(t.Id) && t.UserId == user && t.AccountId == session.AccountId &&
                !t.IsReconciled && t.Status == TransactionStatus.Posted && t.Date <= session.StatementDate)
            .Select(t => new { t.Id, t.Amount }).ToListAsync(ct);
        FinanceToolSupport.Check(rows.Count == ids.Count, "Selection includes an ineligible, locked or missing transaction.");
        return new(id, ids, rows.Sum(t => t.Amount));
    }

    private static async Task<Domain.Entities.Reconciliation> Session(ApplicationDbContext db, int id, string user, CancellationToken ct)
        => await db.Reconciliations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.UserId == user && r.Account.UserId == user, ct)
            ?? throw new ModelContextProtocol.McpException("Reconciliation not found.");
    private static IQueryable<ReconciliationToolDto> Project(IQueryable<Domain.Entities.Reconciliation> rows)
        => rows.Select(r => new ReconciliationToolDto(r.Id, r.AccountId, r.StatementDate, r.StatementBalance, r.OpeningBalance,
            r.ReconciledBalance, r.Difference, r.IsCompleted, r.CompletedAt, r.Notes));
    private static ReconciliationToolDto Map(Domain.Entities.Reconciliation r)
        => new(r.Id, r.AccountId, r.StatementDate, r.StatementBalance, r.OpeningBalance, r.ReconciledBalance, r.Difference, r.IsCompleted, r.CompletedAt, r.Notes);
}

/// <summary>Safe statement session output.</summary>
public sealed record ReconciliationToolDto(int Id, int AccountId, DateTime StatementDate, decimal StatementBalance,
    decimal OpeningBalance, decimal ReconciledBalance, decimal Difference, bool IsCompleted, DateTime? CompletedAt, string? Notes);
/// <summary>Explicit eligible reconciliation selection and signed decimal total.</summary>
public sealed record ReconciliationSelectionToolDto(int ReconciliationId, IReadOnlyList<int> TransactionIds, decimal Total);

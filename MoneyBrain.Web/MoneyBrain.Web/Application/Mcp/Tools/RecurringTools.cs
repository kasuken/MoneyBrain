using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.Transactions.RecurringTransactions;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Recurring template lookup and bounded, explicit generation previews.</summary>
[McpServerToolType]
public sealed class RecurringTools(McpAccess access, IRecurringTransactionService recurring, IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Lists templates in bounded pages without loading all transaction history.</summary>
    [McpServerTool(Name = "recurring_templates_list", ReadOnly = true, OpenWorld = false)]
    [Description("List recurring templates, maximum 100 per page. Create templates with transactions_create IsRecurring=true; generated occurrences remain ordinary transactions.")]
    public async Task<FinancePage<FinanceTransactionDto>> List(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        FinanceToolSupport.Page(offset, limit);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await FinanceProjections.Transactions(db.Transactions.AsNoTracking().Where(t => t.UserId == user && t.Account.UserId == user && t.IsRecurring)
            .OrderBy(t => t.NextRecurrenceDate).ThenBy(t => t.Id).Skip(offset).Take(limit + 1)).ToListAsync(cancellationToken);
        return new(rows.Take(limit).ToList(), offset, rows.Count > limit);
    }

    /// <summary>Previews the exact number of due occurrences within a safe bound.</summary>
    [McpServerTool(Name = "recurring_generate_preview", ReadOnly = true, OpenWorld = false)]
    [Description("Preview due recurring generation through an ISO date (not beyond one year from today). Rejects more than 100 templates/occurrences. Transfer templates are skipped; reconciled templates generate occurrences but are never modified.")]
    public async Task<RecurringGenerationToolDto> Preview(DateTime throughDate, CancellationToken cancellationToken = default)
    {
        var user = access.RequireRead();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await Summarize(db, user, throughDate, cancellationToken);
    }

    /// <summary>Generates the previewed due occurrences through the application service.</summary>
    [McpServerTool(Name = "recurring_generate", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("After recurring_generate_preview, manually generate due occurrences (maximum 100). Check generated transactions after a timeout before retrying.")]
    public async Task<RecurringGenerationToolDto> Generate(DateTime throughDate, int expectedOccurrences, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var user = access.RequireWrite(confirmed);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var summary = await Summarize(db, user, throughDate, cancellationToken);
        FinanceToolSupport.Check(summary.Occurrences == expectedOccurrences, "Generation preview changed. Refresh recurring_generate_preview.");
        var count = await FinanceToolSupport.Run(() => recurring.GenerateDueRecurringTransactionsAsync(user, throughDate, cancellationToken));
        return summary with { Occurrences = count };
    }

    private async Task<RecurringGenerationToolDto> Summarize(ApplicationDbContext db, string user, DateTime through, CancellationToken ct)
    {
        FinanceToolSupport.Check(through.Year >= 1900 && through.Date <= DateTime.UtcNow.Date.AddYears(1), "Generation date is outside the supported range.");
        // Same due-template filter as RecurringTransactionService.GenerateDueRecurringTransactionsAsync.
        var templates = await db.Transactions.AsNoTracking().Where(t => t.UserId == user && t.IsRecurring && t.TransferTransactionId == null &&
                t.RecurrenceFrequency.HasValue && t.RecurrenceStartDate.HasValue && (t.NextRecurrenceDate == null || t.NextRecurrenceDate <= through))
            .OrderBy(t => t.Id).Take(101).ToListAsync(ct);
        FinanceToolSupport.Check(templates.Count <= 100, "Too many due templates. Generate a shorter date range.");
        var lastGenerated = await RecurringTransactionService.GetLastGeneratedDatesAsync(db, user, templates.Select(t => t.Id).ToList(), ct);
        var count = 0;
        foreach (var t in templates)
        {
            FinanceToolSupport.Check(Enum.IsDefined(t.RecurrenceFrequency.GetValueOrDefault()), "A due recurring template has an invalid frequency.");
            await FinanceToolSupport.Account(db, t.AccountId, user, ct);
            FinanceToolSupport.Money(t.Amount);
            await FinanceToolSupport.Category(db, t.CategoryId, user, ct);
            await FinanceToolSupport.Payee(db, t.PayeeId, user, ct);
            var splits = await db.TransactionSplits.Where(s => s.TransactionId == t.Id).Select(s => new { s.Amount, s.CategoryId }).Take(101).ToListAsync(ct);
            FinanceToolSupport.Check(splits.Count <= 100 && (splits.Count == 0 || splits.Sum(s => s.Amount) == t.Amount), "Recurring template split total is invalid or contains too many splits.");
            foreach (var split in splits)
            {
                FinanceToolSupport.Money(split.Amount);
                await FinanceToolSupport.Category(db, split.CategoryId, user, ct);
            }
            var date = RecurringTransactionService.GetNextDueDate(t, lastGenerated.TryGetValue(t.Id, out var last) ? last : null);
            while (date <= through)
            {
                FinanceToolSupport.Check(++count <= 100, "More than 100 occurrences are due. Use an earlier generation date.");
                date = recurring.CalculateNextRecurrenceDate(date, t.RecurrenceFrequency.GetValueOrDefault());
            }
        }
        return new(through, templates.Select(t => t.Id).ToList(), count);
    }
}

/// <summary>A bounded due-generation plan or outcome.</summary>
public sealed record RecurringGenerationToolDto(DateTime ThroughDate, IReadOnlyList<int> TemplateIds, int Occurrences);

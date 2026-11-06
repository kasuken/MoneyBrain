using System.Data;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.RecurringTransactions;

/// <summary>
/// Service for managing recurring (repeating) transactions.
/// Automatically generates transactions based on recurrence patterns.
/// </summary>
public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILedgerService _ledgerService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RecurringTransactionService> _logger;

    public RecurringTransactionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILedgerService ledgerService,
        ICacheService cacheService,
        ILogger<RecurringTransactionService> logger)
    {
        _contextFactory = contextFactory;
        _ledgerService = ledgerService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public DateTime CalculateNextRecurrenceDate(DateTime currentDate, RecurrenceFrequency frequency) =>
        AdvanceDate(currentDate, frequency);

    private static DateTime AdvanceDate(DateTime currentDate, RecurrenceFrequency frequency)
    {
        return frequency switch
        {
            RecurrenceFrequency.Weekly => currentDate.AddDays(7),
            RecurrenceFrequency.Monthly => currentDate.AddMonths(1),
            RecurrenceFrequency.Quarterly => currentDate.AddMonths(3),
            RecurrenceFrequency.SixMonths => currentDate.AddMonths(6),
            RecurrenceFrequency.Yearly => currentDate.AddYears(1),
            _ => throw new ArgumentException($"Unknown recurrence frequency: {frequency}", nameof(frequency))
        };
    }

    /// <summary>
    /// Returns the next occurrence date for a template. Generation resumes after the latest
    /// occurrence already generated, so a retry, an edit that resets the stored next date, or a
    /// reconciled template whose stored date cannot advance never produces duplicate occurrences.
    /// </summary>
    /// <param name="template">A recurring template with a frequency and start date.</param>
    /// <param name="lastGeneratedDate">Date of the latest occurrence generated from it, if any.</param>
    public static DateTime GetNextDueDate(Transaction template, DateTime? lastGeneratedDate)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.RecurrenceFrequency is not { } frequency || template.RecurrenceStartDate is not { } startDate)
            throw new InvalidOperationException("Recurring template requires a frequency and start date");

        var storedNext = template.NextRecurrenceDate ?? startDate;
        if (lastGeneratedDate is not { } lastDate)
            return storedNext;

        var afterLastGenerated = AdvanceDate(lastDate, frequency);
        return afterLastGenerated > storedNext ? afterLastGenerated : storedNext;
    }

    /// <summary>
    /// Latest generated occurrence date per template, computed in SQL without loading history.
    /// </summary>
    public static async Task<Dictionary<int, DateTime>> GetLastGeneratedDatesAsync(
        ApplicationDbContext context,
        string userId,
        IReadOnlyCollection<int> templateIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(templateIds);

        return await context.Transactions
            .Where(t => t.UserId == userId &&
                        t.RecurringTemplateId.HasValue &&
                        templateIds.Contains(t.RecurringTemplateId.Value))
            .GroupBy(t => t.RecurringTemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, LastDate = g.Max(t => t.Date) })
            .ToDictionaryAsync(x => x.TemplateId, x => x.LastDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GenerateDueRecurringTransactionsAsync(
        string userId,
        DateTime upToDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // One serializable transaction covers the due-date reads, every occurrence, its ledger
        // entries and the template advancement: a failure rolls everything back, and a concurrent
        // or retried run cannot read the same due dates and generate them twice.
        await using var dbTransaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        // Due templates for this user. Transfer legs are excluded because an occurrence could
        // not reproduce the linked transfer pair.
        var recurringTemplates = await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Splits)
            .Where(t =>
                t.UserId == userId &&
                t.IsRecurring &&
                t.TransferTransactionId == null &&
                t.RecurrenceFrequency.HasValue &&
                t.RecurrenceStartDate.HasValue &&
                (t.NextRecurrenceDate == null || t.NextRecurrenceDate.Value <= upToDate))
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        if (recurringTemplates.Count == 0)
            return 0;

        var lastGeneratedDates = await GetLastGeneratedDatesAsync(
            context, userId, recurringTemplates.Select(t => t.Id).ToList(), cancellationToken);

        var generatedCount = 0;
        var generatedMonths = new HashSet<DateTime>();

        foreach (var template in recurringTemplates)
        {
            var nextDate = GetNextDueDate(
                template,
                lastGeneratedDates.TryGetValue(template.Id, out var lastGenerated) ? lastGenerated : null);

            // Generate all occurrences up to upToDate
            while (nextDate <= upToDate)
            {
                // Create new transaction instance from template
                var newTransaction = new Transaction
                {
                    UserId = template.UserId,
                    AccountId = template.AccountId,
                    Account = template.Account,
                    Date = nextDate,
                    Amount = template.Amount,
                    PayeeId = template.PayeeId,
                    CategoryId = template.CategoryId,
                    Memo = template.Memo,
                    Status = TransactionStatus.Pending,
                    IsCleared = false,
                    IsReconciled = false,
                    ReferenceNumber = template.ReferenceNumber,
                    Tags = template.Tags,
                    RecurringTemplateId = template.Id,
                    IsRecurring = false, // Generated instances are not themselves recurring
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Copy template splits so the ledger is generated from the same category lines
                foreach (var split in template.Splits)
                {
                    newTransaction.Splits.Add(new TransactionSplit
                    {
                        Amount = split.Amount,
                        CategoryId = split.CategoryId,
                        Memo = split.Memo
                    });
                }

                context.Transactions.Add(newTransaction);
                await context.SaveChangesAsync(cancellationToken);

                // Generate ledger entries
                await _ledgerService.GenerateLedgerEntriesAsync(context, newTransaction, cancellationToken);

                generatedCount++;
                generatedMonths.Add(new DateTime(nextDate.Year, nextDate.Month, 1));

                // Move to next recurrence date
                nextDate = AdvanceDate(nextDate, template.RecurrenceFrequency!.Value);
            }

            // Reconciled templates are immutable: their schedule position is derived from the
            // generated occurrences instead of being written back to the locked row.
            if (!template.IsReconciled && template.NextRecurrenceDate != nextDate)
            {
                template.NextRecurrenceDate = nextDate;
                template.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        // After commit only, so a rolled-back run never evicts caches for data that did not change.
        foreach (var month in generatedMonths)
            await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, month);

        if (generatedCount > 0)
        {
            _logger.LogInformation(
                "Generated {GeneratedCount} recurring transactions from {TemplateCount} templates through {UpToDate}",
                generatedCount,
                recurringTemplates.Count,
                upToDate);
        }

        return generatedCount;
    }

    /// <inheritdoc />
    public async Task<List<Transaction>> GetRecurringTemplatesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .Where(t => t.UserId == userId && t.IsRecurring)
            .OrderBy(t => t.NextRecurrenceDate)
            .ToListAsync(cancellationToken);
    }
}
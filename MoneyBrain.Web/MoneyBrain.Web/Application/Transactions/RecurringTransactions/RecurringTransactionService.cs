using Microsoft.EntityFrameworkCore;
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
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<RecurringTransactionService> _logger;

    public RecurringTransactionService(
        ApplicationDbContext context,
        ILedgerService ledgerService,
        ILogger<RecurringTransactionService> logger)
    {
        _context = context;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    /// <inheritdoc />
    public DateTime CalculateNextRecurrenceDate(DateTime currentDate, RecurrenceFrequency frequency)
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

    /// <inheritdoc />
    public async Task<int> GenerateDueRecurringTransactionsAsync(
        string userId,
        DateTime upToDate,
        CancellationToken cancellationToken = default)
    {
        // Find all recurring transaction templates where:
        // 1. IsRecurring = true
        // 2. NextRecurrenceDate <= upToDate (or is null and needs initialization)
        // 3. User matches
        var recurringTemplates = await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Splits)
            .Where(t =>
                t.UserId == userId &&
                t.IsRecurring &&
                t.RecurrenceFrequency.HasValue &&
                t.RecurrenceStartDate.HasValue &&
                (t.NextRecurrenceDate == null || t.NextRecurrenceDate.Value <= upToDate))
            .ToListAsync(cancellationToken);

        var generatedCount = 0;

        foreach (var template in recurringTemplates)
        {
            // Initialize NextRecurrenceDate if null
            if (template.NextRecurrenceDate == null)
            {
                template.NextRecurrenceDate = template.RecurrenceStartDate!.Value;
            }

            // Generate all occurrences up to upToDate
            while (template.NextRecurrenceDate!.Value <= upToDate)
            {
                // Create new transaction instance from template
                var newTransaction = new Transaction
                {
                    UserId = template.UserId,
                    AccountId = template.AccountId,
                    Date = template.NextRecurrenceDate.Value,
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

                _context.Transactions.Add(newTransaction);
                await _context.SaveChangesAsync(cancellationToken);

                // Load Account navigation for ledger generation
                await _context.Entry(newTransaction)
                    .Reference(t => t.Account)
                    .LoadAsync(cancellationToken);

                // Handle splits if template has them
                if (template.Splits.Any())
                {
                    foreach (var split in template.Splits)
                    {
                        var newSplit = new TransactionSplit
                        {
                            TransactionId = newTransaction.Id,
                            Amount = split.Amount,
                            CategoryId = split.CategoryId,
                            Memo = split.Memo
                        };
                        _context.TransactionSplits.Add(newSplit);
                    }
                    await _context.SaveChangesAsync(cancellationToken);

                    // Reload splits for ledger generation
                    await _context.Entry(newTransaction)
                        .Collection(t => t.Splits)
                        .LoadAsync(cancellationToken);
                }

                // Generate ledger entries
                await _ledgerService.GenerateLedgerEntriesAsync(newTransaction, cancellationToken);

                _logger.LogInformation(
                    "Generated recurring transaction {TransactionId} from template {TemplateId} for date {Date}",
                    newTransaction.Id,
                    template.Id,
                    newTransaction.Date);

                generatedCount++;

                // Move to next recurrence date
                template.NextRecurrenceDate = CalculateNextRecurrenceDate(
                    template.NextRecurrenceDate.Value,
                    template.RecurrenceFrequency!.Value);
            }

            template.UpdatedAt = DateTime.UtcNow;
        }

        if (generatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return generatedCount;
    }

    /// <inheritdoc />
    public async Task<List<Transaction>> GetRecurringTemplatesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .Where(t => t.UserId == userId && t.IsRecurring)
            .OrderBy(t => t.NextRecurrenceDate)
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.CreditCardBilling;

/// <summary>
/// Service for handling credit card billing cycle operations.
/// </summary>
public class CreditCardBillingService : ICreditCardBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<CreditCardBillingService> _logger;

    public CreditCardBillingService(
        ApplicationDbContext context,
        ILedgerService ledgerService,
        ILogger<CreditCardBillingService> logger)
    {
        _context = context;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsDueForBillingAsync(
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // Pull credit cards that potentially match today's billing day.
        // We fetch all active credit cards with a LinkedPaymentAccountId and then
        // resolve the per-user local date to handle billing-day comparisons correctly
        // regardless of the user's timezone (stored in UserSettings.TimeZoneId).
        var candidates = await _context.Accounts
            .Where(a => a.SubType == AccountSubType.CreditCard &&
                        a.BillingCycleDay.HasValue &&
                        a.LinkedPaymentAccountId.HasValue &&
                        a.IsActive)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return [];

        // Load timezone settings for the distinct users that own these accounts.
        var userIds = candidates.Select(a => a.UserId).Distinct().ToList();
        var settingsByUser = await _context.UserSettings
            .Where(us => userIds.Contains(us.UserId))
            .AsNoTracking()
            .ToDictionaryAsync(us => us.UserId, cancellationToken);

        var due = new List<Account>();

        foreach (var account in candidates)
        {
            var localToday = ResolveLocalDate(utcNow, account.UserId, settingsByUser);
            var currentMonth = new DateTime(localToday.Year, localToday.Month, 1);

            if (account.BillingCycleDay == localToday.Day &&
                (account.LastBillingCycleDate == null || account.LastBillingCycleDate < currentMonth))
            {
                due.Add(account);
            }
        }

        return due;
    }

    public async Task<BillingCycleResult> ProcessBillingCycleAsync(
        int creditCardAccountId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Resolve the user's local "today" for billing-day and billing-month labelling.
        var userSettings = await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        var settingsMap = userSettings != null
            ? new Dictionary<string, Domain.Entities.UserSettings> { [userId] = userSettings }
            : new Dictionary<string, Domain.Entities.UserSettings>();

        var today = ResolveLocalDate(DateTime.UtcNow, userId, settingsMap);
        var billingMonth = $"{today:yyyy-MM}";

        _logger.LogInformation(
            "Processing billing cycle for credit card {AccountId}, billing month {BillingMonth}",
            creditCardAccountId, billingMonth);

        // Get the credit card account with all necessary data
        var creditCard = await _context.Accounts
            .Include(a => a.LinkedPaymentAccount)
            .FirstOrDefaultAsync(a => a.Id == creditCardAccountId && a.UserId == userId, cancellationToken);

        if (creditCard == null)
        {
            return new BillingCycleResult
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = "Unknown",
                Success = false,
                ErrorMessage = "Credit card account not found",
                BillingCycleMonth = billingMonth
            };
        }

        if (creditCard.SubType != AccountSubType.CreditCard)
        {
            return new BillingCycleResult
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = creditCard.Name,
                Success = false,
                ErrorMessage = "Account is not a credit card",
                BillingCycleMonth = billingMonth
            };
        }

        if (!creditCard.LinkedPaymentAccountId.HasValue)
        {
            return new BillingCycleResult
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = creditCard.Name,
                Success = false,
                ErrorMessage = "No linked payment account configured",
                BillingCycleMonth = billingMonth
            };
        }

        // Use a transaction to ensure atomicity
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Get all pending transactions for this credit card
            var pendingTransactions = await _context.Transactions
                .Where(t => t.AccountId == creditCardAccountId &&
                            t.UserId == userId &&
                            t.Status == TransactionStatus.Pending &&
                            !t.IsReconciled)
                .ToListAsync(cancellationToken);

            if (pendingTransactions.Count == 0)
            {
                _logger.LogInformation(
                    "No pending transactions to process for credit card {AccountId}",
                    creditCardAccountId);

                // Still update LastBillingCycleDate to prevent reprocessing
                creditCard.LastBillingCycleDate = today;
                await _context.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                return new BillingCycleResult
                {
                    CreditCardAccountId = creditCardAccountId,
                    CreditCardAccountName = creditCard.Name,
                    Success = true,
                    TransactionsPosted = 0,
                    TotalBilledAmount = 0,
                    BillingCycleMonth = billingMonth
                };
            }

            // 2. Mark all pending transactions as posted
            var totalAmount = 0m;
            foreach (var transaction in pendingTransactions)
            {
                transaction.Status = TransactionStatus.Posted;
                transaction.BillingCycleMonth = billingMonth;
                transaction.UpdatedAt = DateTime.UtcNow;
                totalAmount += transaction.Amount;
            }

            // 3. Create consolidated billing transaction in the linked payment account
            // The billing amount is the total of expenses (negative) on the credit card
            // This creates a pending expense in the payment account
            var billingTransaction = new Transaction
            {
                UserId = userId,
                AccountId = creditCard.LinkedPaymentAccountId.Value,
                Date = today,
                Amount = totalAmount, // Should be negative (expense)
                Memo = $"Credit Card Bill - {creditCard.Name} ({billingMonth})",
                Status = TransactionStatus.Pending, // Bill starts as pending
                IsCleared = false,
                CreditCardBillingSourceAccountId = creditCardAccountId,
                BillingCycleMonth = billingMonth,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(billingTransaction);
            await _context.SaveChangesAsync(cancellationToken);

            // Load the account for ledger entry generation
            await _context.Entry(billingTransaction)
                .Reference(t => t.Account)
                .LoadAsync(cancellationToken);

            // Generate ledger entries for the billing transaction
            await _ledgerService.GenerateLedgerEntriesAsync(billingTransaction, cancellationToken);

            // 4. Update LastBillingCycleDate on the credit card
            creditCard.LastBillingCycleDate = today;
            creditCard.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully processed billing cycle for credit card {AccountId}: {Count} transactions posted, total amount {Amount}",
                creditCardAccountId, pendingTransactions.Count, totalAmount);

            return new BillingCycleResult
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = creditCard.Name,
                Success = true,
                TransactionsPosted = pendingTransactions.Count,
                TotalBilledAmount = totalAmount,
                BillingTransactionId = billingTransaction.Id,
                BillingCycleMonth = billingMonth
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing billing cycle for credit card {AccountId}",
                creditCardAccountId);

            await dbTransaction.RollbackAsync(cancellationToken);

            return new BillingCycleResult
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = creditCard.Name,
                Success = false,
                ErrorMessage = ex.Message,
                BillingCycleMonth = billingMonth
            };
        }
    }

    public async Task<IReadOnlyList<BillingCycleResult>> ProcessAllDueBillingCyclesAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<BillingCycleResult>();
        var accountsDue = await GetAccountsDueForBillingAsync(cancellationToken);

        foreach (var account in accountsDue)
        {
            var result = await ProcessBillingCycleAsync(account.Id, account.UserId, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<BillingCyclePreview> GetBillingCyclePreviewAsync(
        int creditCardAccountId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var creditCard = await _context.Accounts
            .Include(a => a.LinkedPaymentAccount)
            .FirstOrDefaultAsync(a => a.Id == creditCardAccountId && a.UserId == userId, cancellationToken);

        if (creditCard == null)
        {
            return new BillingCyclePreview
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = "Unknown",
                CanProcess = false,
                ValidationMessage = "Credit card account not found"
            };
        }

        if (creditCard.SubType != AccountSubType.CreditCard)
        {
            return new BillingCyclePreview
            {
                CreditCardAccountId = creditCardAccountId,
                CreditCardAccountName = creditCard.Name,
                CanProcess = false,
                ValidationMessage = "Account is not a credit card"
            };
        }

        var pendingTransactions = await _context.Transactions
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .Where(t => t.AccountId == creditCardAccountId &&
                        t.UserId == userId &&
                        t.Status == TransactionStatus.Pending &&
                        !t.IsReconciled)
            .OrderByDescending(t => t.Date)
            .ToListAsync(cancellationToken);

        var totalPending = pendingTransactions.Sum(t => t.Amount);

        var canProcess = creditCard.LinkedPaymentAccountId.HasValue;
        var validationMessage = !canProcess
            ? "No linked payment account configured. Please edit the credit card account to set a payment account."
            : null;

        return new BillingCyclePreview
        {
            CreditCardAccountId = creditCardAccountId,
            CreditCardAccountName = creditCard.Name,
            LinkedPaymentAccountId = creditCard.LinkedPaymentAccountId,
            LinkedPaymentAccountName = creditCard.LinkedPaymentAccount?.Name,
            PendingTransactionCount = pendingTransactions.Count,
            TotalPendingAmount = totalPending,
            PendingTransactions = pendingTransactions,
            CanProcess = canProcess,
            ValidationMessage = validationMessage
        };
    }

    /// <summary>
    /// Returns the calendar date in the user's configured timezone.
    /// Falls back to UTC if the timezone ID is unknown or unavailable on this platform.
    /// </summary>
    private static DateTime ResolveLocalDate(
        DateTime utcNow,
        string userId,
        Dictionary<string, UserSettings> settingsByUser)
    {
        if (settingsByUser.TryGetValue(userId, out var settings) &&
            !string.IsNullOrWhiteSpace(settings.TimeZoneId))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz).Date;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Unknown or corrupt timezone ID — fall through to UTC.
            }
        }

        return utcNow.Date; // UTC fallback
    }
}

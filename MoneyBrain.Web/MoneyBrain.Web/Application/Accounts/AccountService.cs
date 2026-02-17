using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Accounts;

/// <summary>
/// Service for managing user accounts (assets and liabilities).
/// Enforces business rules and user ownership.
/// </summary>
public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountService> _logger;
    private readonly ILedgerService _ledgerService;
    private readonly ICacheService _cacheService;

    public AccountService(
        ApplicationDbContext context, 
        ILogger<AccountService> logger,
        ILedgerService ledgerService,
        ICacheService cacheService)
    {
        _context = context;
        _logger = logger;
        _ledgerService = ledgerService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<Account>> GetUserAccountsAsync(
        string userId, 
        bool includeInactive = false, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Accounts
            .Where(a => a.UserId == userId);

        if (includeInactive)
        {
            return await query
                .OrderBy(a => a.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        var cacheKey = CacheKeyHelper.ForUserAccounts(userId);
        var cached = await _cacheService.GetAsync<IReadOnlyList<Account>>(cacheKey);
        if (cached != null)
            return cached;

        var result = await query
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));

        return result;
    }

    public async Task<Account?> GetAccountByIdAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);
    }

    public async Task<Account> CreateAccountAsync(
        Account account, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (string.IsNullOrWhiteSpace(account.UserId))
        {
            throw new InvalidOperationException("Account must belong to a user.");
        }

        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = null;

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserAccounts(account.UserId));

        _logger.LogInformation(
            "Account created: {AccountId} - {AccountName} for user {UserId}", 
            account.Id, 
            account.Name, 
            account.UserId);

        return account;
    }

    public async Task<Account> UpdateAccountAsync(
        Account account, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var existing = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == account.Id && a.UserId == account.UserId, 
                cancellationToken);

        if (existing == null)
        {
            throw new InvalidOperationException($"Account {account.Id} not found for user {account.UserId}.");
        }

        // Update mutable fields
        existing.Name = account.Name;
        existing.Type = account.Type;
        existing.SubType = account.SubType;
        existing.Group = account.Group;
        existing.Notes = account.Notes;
        existing.IsActive = account.IsActive;
        existing.CurrencyCode = account.CurrencyCode;
        existing.MonthlySpendingLimit = account.MonthlySpendingLimit;
        existing.BillingCycleDay = account.BillingCycleDay;
        existing.LinkedPaymentAccountId = account.LinkedPaymentAccountId;
        existing.UpdatedAt = DateTime.UtcNow;

        // OpeningBalance should NOT be updated directly - must use AdjustOpeningBalanceAsync
        // This enforces the audit trail requirement from the PRD
        if (existing.OpeningBalance != account.OpeningBalance)
        {
            _logger.LogWarning(
                "Attempted to change opening balance directly for account {AccountId}. Use AdjustOpeningBalanceAsync instead.",
                account.Id);
            throw new InvalidOperationException("Opening balance cannot be changed directly. Use AdjustOpeningBalanceAsync to maintain audit trail.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserAccounts(account.UserId));

        _logger.LogInformation(
            "Account updated: {AccountId} - {AccountName}", 
            account.Id, 
            account.Name);

        return existing;
    }

    public async Task<bool> DeactivateAccountAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            return false;
        }

        account.IsActive = false;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserAccounts(userId));

        _logger.LogInformation(
            "Account deactivated: {AccountId} - {AccountName}", 
            accountId, 
            account.Name);

        return true;
    }

    public async Task<bool> DeleteAccountAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            return false;
        }

        // TODO: Check if account has transactions before allowing deletion
        // For v1, allow deletion; add transaction check in future iteration

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserAccounts(userId));

        _logger.LogInformation(
            "Account deleted: {AccountId} - {AccountName}", 
            accountId, 
            account.Name);

        return true;
    }

    public async Task<Account> AdjustOpeningBalanceAsync(
        int accountId, 
        decimal newBalance, 
        string? reason, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        var previousBalance = account.OpeningBalance;
        var adjustmentAmount = newBalance - previousBalance;

        // Only create adjustment record if the balance actually changed
        if (adjustmentAmount != 0)
        {
            var adjustment = new OpeningBalanceAdjustment
            {
                AccountId = accountId,
                PreviousBalance = previousBalance,
                NewBalance = newBalance,
                AdjustmentAmount = adjustmentAmount,
                Reason = reason,
                AdjustedAt = DateTime.UtcNow,
                AdjustedByUserId = userId
            };

            account.OpeningBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;

            _context.OpeningBalanceAdjustments.Add(adjustment);
            
            // Automatically create a balance snapshot after opening balance adjustment
            var snapshot = new AccountBalanceSnapshot
            {
                AccountId = accountId,
                SnapshotDate = DateTime.UtcNow,
                Balance = newBalance,
                Type = SnapshotType.OpeningBalanceAdjustment,
                Notes = $"After opening balance adjustment: {reason ?? "(no reason provided)"}",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.AccountBalanceSnapshots.Add(snapshot);
            
            await _context.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveAsync(CacheKeyHelper.ForUserAccounts(userId));

            _logger.LogInformation(
                "Opening balance adjusted for account {AccountId} from {PreviousBalance} to {NewBalance} (Δ {Adjustment}). Reason: {Reason}",
                accountId,
                previousBalance,
                newBalance,
                adjustmentAmount,
                reason ?? "(none provided)");
        }
        else
        {
            _logger.LogDebug(
                "Opening balance adjustment requested for account {AccountId} but new balance equals current balance ({Balance}).",
                accountId,
                newBalance);
        }

        return account;
    }

    public async Task<IReadOnlyList<OpeningBalanceAdjustment>> GetOpeningBalanceAdjustmentsAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        return await _context.OpeningBalanceAdjustments
            .Where(oba => oba.AccountId == accountId)
            .OrderByDescending(oba => oba.AdjustedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountBalanceSnapshot> CreateBalanceSnapshotAsync(
        int accountId, 
        decimal balance, 
        DateTime snapshotDate, 
        SnapshotType type, 
        string? notes, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        var snapshot = new AccountBalanceSnapshot
        {
            AccountId = accountId,
            SnapshotDate = snapshotDate,
            Balance = balance,
            Type = type,
            Notes = notes,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccountBalanceSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Balance snapshot created for account {AccountId}: {Balance} at {SnapshotDate} (Type: {Type})",
            accountId,
            balance,
            snapshotDate,
            type);

        return snapshot;
    }

    public async Task<IReadOnlyList<AccountBalanceSnapshot>> GetBalanceHistoryAsync(
        int accountId, 
        string userId, 
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        var query = _context.AccountBalanceSnapshots
            .Where(abs => abs.AccountId == accountId);

        if (startDate.HasValue)
        {
            query = query.Where(abs => abs.SnapshotDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(abs => abs.SnapshotDate <= endDate.Value);
        }

        return await query
            .OrderBy(abs => abs.SnapshotDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> CalculateCurrentBalanceAsync(
        int accountId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        // Use ledger-based balance calculation (double-entry bookkeeping)
        // This includes opening balance + ledger entries + manual adjustments
        var currentBalance = await _ledgerService.GetAccountBalanceAsync(
            accountId, 
            userId, 
            asOfDate: null, // Current balance
            cancellationToken);

        _logger.LogDebug(
            "Calculated current balance for account {AccountId} using double-entry bookkeeping: {Balance}",
            accountId,
            currentBalance);

        return currentBalance;
    }

    public async Task<ManualBalanceAdjustment> CreateManualAdjustmentAsync(
        int accountId, 
        decimal amount, 
        DateTime adjustmentDate, 
        string description, 
        string? category, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required for manual balance adjustments.", nameof(description));
        }

        var adjustment = new ManualBalanceAdjustment
        {
            AccountId = accountId,
            Amount = amount,
            AdjustmentDate = adjustmentDate,
            Description = description,
            Category = category,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsReconciled = false
        };

        _context.ManualBalanceAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Manual adjustment created for account {AccountId}: {Amount} on {Date}. Description: {Description}",
            accountId,
            amount,
            adjustmentDate,
            description);

        return adjustment;
    }

    public async Task<IReadOnlyList<ManualBalanceAdjustment>> GetManualAdjustmentsAsync(
        int accountId, 
        string userId, 
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        CancellationToken cancellationToken = default)
    {
        // Verify the user owns the account
        var accountExists = await _context.Accounts
            .AnyAsync(
                a => a.Id == accountId && a.UserId == userId, 
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        var query = _context.ManualBalanceAdjustments
            .Where(mba => mba.AccountId == accountId);

        if (startDate.HasValue)
        {
            query = query.Where(mba => mba.AdjustmentDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(mba => mba.AdjustmentDate <= endDate.Value);
        }

        return await query
            .OrderByDescending(mba => mba.AdjustmentDate)
            .ThenByDescending(mba => mba.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteManualAdjustmentAsync(
        int adjustmentId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var adjustment = await _context.ManualBalanceAdjustments
            .Include(mba => mba.Account)
            .FirstOrDefaultAsync(
                mba => mba.Id == adjustmentId, 
                cancellationToken);

        if (adjustment == null)
        {
            return false;
        }

        // Verify user owns the account
        if (adjustment.Account?.UserId != userId)
        {
            throw new UnauthorizedAccessException($"User {userId} does not own account {adjustment.AccountId}.");
        }

        // Enforce immutability: cannot delete reconciled adjustments
        if (adjustment.IsReconciled)
        {
            throw new InvalidOperationException("Cannot delete a reconciled adjustment. Reconciled data is immutable.");
        }

        _context.ManualBalanceAdjustments.Remove(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Manual adjustment deleted: {AdjustmentId} for account {AccountId}",
            adjustmentId,
            adjustment.AccountId);

        return true;
    }

    public async Task<MonthlySpendingSummary> GetMonthlySpendingAsync(
        int accountId,
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == accountId && a.UserId == userId,
                cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        // Calculate date range for the month
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        // Query transactions based on account type:
        // - Credit cards: Only PENDING (unbilled charges), exclude Posted (already billed)
        // - Other accounts: Both Posted AND Pending (real-time spending tracking)
        var transactionQuery = _context.Transactions
            .Where(t => t.AccountId == accountId
                        && t.UserId == userId
                        && t.TransferTransactionId == null
                        && t.Date >= startDate
                        && t.Date < endDate);

        if (account.SubType == AccountSubType.CreditCard)
        {
            // Credit cards: Only count pending (unbilled) transactions
            transactionQuery = transactionQuery.Where(t => t.Status == TransactionStatus.Pending);
        }
        else
        {
            // Other accounts: Count both posted and pending
            transactionQuery = transactionQuery.Where(t =>
                t.Status == TransactionStatus.Posted
                || t.Status == TransactionStatus.Pending);
        }

        var transactions = await transactionQuery
            .Select(t => t.Amount)
            .ToListAsync(cancellationToken);

        // Calculate spending based on account type
        decimal spentAmount;

        if (account.Type == AccountType.Asset)
        {
            // For assets (debit/checking): spending = sum of expenses (negative amounts)
            spentAmount = transactions.Where(a => a < 0).Sum(a => Math.Abs(a));
        }
        else
        {
            // For liabilities (credit cards): net spending = charges minus credits/refunds
            // Charges are negative (money going out), credits/refunds are positive
            decimal charges = 0;
            decimal credits = 0;

            foreach (var transactionAmount in transactions)
            {
                if (transactionAmount < 0)
                {
                    charges += Math.Abs(transactionAmount);
                }
                else if (transactionAmount > 0)
                {
                    credits += transactionAmount;
                }
            }

            spentAmount = Math.Max(0, charges - credits);
        }

        // Calculate remaining and percentage
        decimal? remaining = null;
        decimal? percentUsed = null;
        var isOverLimit = false;

        if (account.MonthlySpendingLimit.HasValue && account.MonthlySpendingLimit.Value > 0)
        {
            remaining = account.MonthlySpendingLimit.Value - spentAmount;
            percentUsed = (spentAmount / account.MonthlySpendingLimit.Value) * 100;
            isOverLimit = spentAmount > account.MonthlySpendingLimit.Value;
        }

        _logger.LogDebug(
            "Monthly spending for account {AccountId} ({Year}/{Month}): {Spent} / {Limit}",
            accountId,
            year,
            month,
            spentAmount,
            account.MonthlySpendingLimit);

        return new MonthlySpendingSummary(
            spentAmount,
            account.MonthlySpendingLimit,
            remaining,
            percentUsed,
            isOverLimit);
    }
}

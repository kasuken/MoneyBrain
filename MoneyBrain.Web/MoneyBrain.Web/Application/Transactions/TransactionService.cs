using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Application.Transactions.FlagManagement;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Transactions.StatusManagement;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions;

public class TransactionService : ITransactionService
{
    /// <summary>
    /// Tolerance used when validating that split amounts sum to the transaction total.
    /// A value of 0.01 accommodates rounding in 2-decimal currencies (e.g. USD, EUR).
    /// Currencies with more decimal places (e.g. KWD – 3 decimals) may accumulate
    /// larger rounding errors and would require a tighter tolerance.
    /// </summary>
    private const decimal SplitAmountTolerance = 0.01m;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILedgerService _ledgerService;
    private readonly ICacheService _cacheService;

    public TransactionService(IDbContextFactory<ApplicationDbContext> contextFactory, ILedgerService ledgerService, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _ledgerService = ledgerService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Normalizes the transaction amount based on the category's type (Income/Expense).
    /// Income categories enforce positive amounts, Expense categories enforce negative amounts.
    /// </summary>
    private async Task<decimal> NormalizeAmountByCategoryTypeAsync(ApplicationDbContext context, decimal amount, int? categoryId, CancellationToken cancellationToken = default)
    {
        // If no category, return amount as-is
        if (!categoryId.HasValue)
            return amount;

        // Look up category and its group type
        var category = await context.Categories
            .Include(c => c.CategoryGroup)
            .FirstOrDefaultAsync(c => c.Id == categoryId.Value, cancellationToken);

        if (category?.CategoryGroup == null)
            return amount;

        // Enforce sign based on category type
        var absoluteAmount = Math.Abs(amount);
        return category.CategoryGroup.Type == CategoryType.Income
            ? absoluteAmount  // Income: always positive
            : -absoluteAmount; // Expense: always negative
    }

    private static IQueryable<Transaction> ApplyDateRange(
        IQueryable<Transaction> query,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        return query;
    }

    public async Task<List<Transaction>> GetAccountTransactionsAsync(int accountId, string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplyDateRange(
            context.Transactions
            .IncludeTransactionDetails()
            .Where(t => t.AccountId == accountId && t.UserId == userId),
            startDate,
            endDate);

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplyDateRange(
            context.Transactions
            .IncludeTransactionDetails()
            .Where(t => t.UserId == userId),
            startDate,
            endDate);

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int transactionId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Transactions
            .IncludeTransactionDetails()
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);
    }

    public async Task<Transaction> CreateTransactionAsync(
        string userId,
        int accountId,
        DateTime date,
        decimal amount,
        int? payeeId,
        int? categoryId,
        string? memo,
        TransactionStatus status,
        bool isCleared,
        string? referenceNumber,
        string? tags,
        bool isRecurring = false,
        RecurrenceFrequency? recurrenceFrequency = null,
        DateTime? recurrenceStartDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Verify account belongs to user
        var accountExists = await context.Accounts
            .AnyAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (!accountExists)
            throw new InvalidOperationException("Account not found or does not belong to user");

        // Normalize amount based on category type (Income = positive, Expense = negative)
        var normalizedAmount = await NormalizeAmountByCategoryTypeAsync(context, amount, categoryId, cancellationToken);

        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = accountId,
            Date = date,
            Amount = normalizedAmount,
            PayeeId = payeeId,
            CategoryId = categoryId,
            Memo = memo,
            Status = status,
            IsCleared = isCleared,
            ReferenceNumber = referenceNumber,
            Tags = tags,
            IsRecurring = isRecurring,
            RecurrenceFrequency = recurrenceFrequency,
            RecurrenceStartDate = recurrenceStartDate,
            NextRecurrenceDate = isRecurring ? recurrenceStartDate : null
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        // Load the Account navigation property for ledger entry generation
        await context.Entry(transaction)
            .Reference(t => t.Account)
            .LoadAsync(cancellationToken);

        // Generate ledger entries for double-entry bookkeeping
        await _ledgerService.GenerateLedgerEntriesAsync(context, transaction, cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, date);

        return transaction;
    }

    public async Task<bool> UpdateTransactionAsync(
        int transactionId,
        string userId,
        DateTime date,
        decimal amount,
        int? payeeId,
        int? categoryId,
        string? memo,
        TransactionStatus status,
        bool isCleared,
        string? referenceNumber,
        string? tags,
        bool isRecurring = false,
        RecurrenceFrequency? recurrenceFrequency = null,
        DateTime? recurrenceStartDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction");

        // Normalize amount based on category type (Income = positive, Expense = negative)
        var normalizedAmount = await NormalizeAmountByCategoryTypeAsync(context, amount, categoryId, cancellationToken);

        transaction.Date = date;
        transaction.Amount = normalizedAmount;
        transaction.PayeeId = payeeId;
        transaction.CategoryId = categoryId;
        transaction.Memo = memo;
        transaction.Status = status;
        transaction.IsCleared = isCleared;
        transaction.ReferenceNumber = referenceNumber;
        transaction.Tags = tags;
        transaction.IsRecurring = isRecurring;
        transaction.RecurrenceFrequency = recurrenceFrequency;
        transaction.RecurrenceStartDate = recurrenceStartDate;
        
        // Update NextRecurrenceDate if recurrence settings changed
        if (isRecurring && recurrenceStartDate.HasValue)
        {
            transaction.NextRecurrenceDate = recurrenceStartDate.Value;
        }
        else if (!isRecurring)
        {
            transaction.NextRecurrenceDate = null;
        }
        
        transaction.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        // Load the Account navigation property for ledger entry regeneration
        await context.Entry(transaction)
            .Reference(t => t.Account)
            .LoadAsync(cancellationToken);

        // Regenerate ledger entries to reflect the updated transaction
        await _ledgerService.RegenerateLedgerEntriesAsync(context, transaction, cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, date);

        return true;
    }

    public async Task<bool> DeleteTransactionAsync(int transactionId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot delete reconciled transaction");

        var transactionDate = transaction.Date;

        // Delete ledger entries first (cascade delete would handle this, but explicit is clearer)
        await _ledgerService.DeleteLedgerEntriesAsync(context, transactionId, cancellationToken);

        context.Transactions.Remove(transaction);
        await context.SaveChangesAsync(cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, transactionDate);

        return true;
    }

    public async Task<List<Transaction>> SearchTransactionsAsync(string userId, TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(filter);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Transactions
            .IncludeTransactionDetails()
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .Include(t => t.TransferTransaction)
                .ThenInclude(tt => tt!.Account)
            .Where(t => t.UserId == userId);

        // Account filter
        if (filter.AccountId.HasValue)
        {
            query = query.Where(t => t.AccountId == filter.AccountId.Value);
        }

        // Date range filter
        if (filter.StartDate.HasValue)
        {
            query = query.Where(t => t.Date >= filter.StartDate.Value);
        }
        if (filter.EndDate.HasValue)
        {
            query = query.Where(t => t.Date <= filter.EndDate.Value);
        }

        // Amount range filter
        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);
        }
        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
        }

        // Transaction type filter
        if (filter.TransactionType.HasValue)
        {
            switch (filter.TransactionType.Value)
            {
                case Filtering.TransactionType.Income:
                    query = query.Where(t => t.Amount > 0 && t.TransferTransactionId == null);
                    break;
                case Filtering.TransactionType.Expense:
                    query = query.Where(t => t.Amount < 0 && t.TransferTransactionId == null);
                    break;
                case Filtering.TransactionType.Transfer:
                    query = query.Where(t => t.TransferTransactionId != null);
                    break;
            }
        }

        // Category filter
        if (filter.CategoryIds != null && filter.CategoryIds.Count > 0)
        {
            query = query.Where(t => t.CategoryId.HasValue && filter.CategoryIds.Contains(t.CategoryId.Value));
        }

        // Payee filter
        if (filter.PayeeIds != null && filter.PayeeIds.Count > 0)
        {
            query = query.Where(t => t.PayeeId.HasValue && filter.PayeeIds.Contains(t.PayeeId.Value));
        }

        // Status filter
        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.Status == filter.Status.Value);
        }

        // Cleared filter
        if (filter.IsCleared.HasValue)
        {
            query = query.Where(t => t.IsCleared == filter.IsCleared.Value);
        }

        // Reconciled filter
        if (filter.IsReconciled.HasValue)
        {
            query = query.Where(t => t.IsReconciled == filter.IsReconciled.Value);
        }

        // Transfer filter
        if (!filter.IncludeTransfers)
        {
            query = query.Where(t => t.TransferTransactionId == null);
        }

        // Tags filter
        if (filter.Tags != null && filter.Tags.Count > 0)
        {
            foreach (var tag in filter.Tags)
            {
                var tagLower = tag.ToLower();
                query = query.Where(t => t.Tags != null && t.Tags.ToLower().Contains(tagLower));
            }
        }

        // Text search filter (payee, memo, category, reference number)
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var searchLower = filter.SearchText.ToLower();
            query = query.Where(t =>
                (t.Payee != null && t.Payee.Name.ToLower().Contains(searchLower)) ||
                (t.Memo != null && t.Memo.ToLower().Contains(searchLower)) ||
                (t.Category != null && t.Category.Name.ToLower().Contains(searchLower)) ||
                (t.ReferenceNumber != null && t.ReferenceNumber.ToLower().Contains(searchLower)));
        }

        // Order by date descending, then ID descending for consistent pagination
        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BulkEditResult> BulkUpdateTransactionsAsync(string userId, BulkEditTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        var result = new BulkEditResult();

        if (!request.HasUpdates || request.TransactionIds.Count == 0)
        {
            return result;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Get all transactions to update
        var transactions = await context.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        // Separate reconciled from non-reconciled
        var reconciledIds = transactions
            .Where(t => t.IsReconciled)
            .Select(t => t.Id)
            .ToList();

        var transactionsToUpdate = transactions
            .Where(t => !t.IsReconciled)
            .ToList();

        // Track skipped reconciled transactions
        if (reconciledIds.Count > 0)
        {
            result.SkippedTransactionIds = reconciledIds;
            result.SkipReason = "Reconciled transactions cannot be modified";
        }

        // Apply updates to non-reconciled transactions
        foreach (var transaction in transactionsToUpdate)
        {
            var updated = false;

            // Update category
            if (request.ClearCategory)
            {
                transaction.CategoryId = null;
                updated = true;
            }
            else if (request.CategoryId.HasValue)
            {
                transaction.CategoryId = request.CategoryId.Value;
                updated = true;
            }

            // Update payee
            if (request.ClearPayee)
            {
                transaction.PayeeId = null;
                updated = true;
            }
            else if (request.PayeeId.HasValue)
            {
                transaction.PayeeId = request.PayeeId.Value;
                updated = true;
            }

            // Update tags
            if (request.ClearTags)
            {
                transaction.Tags = null;
                updated = true;
            }
            else if (request.Tags != null)
            {
                transaction.Tags = request.Tags.Count > 0 ? string.Join(",", request.Tags) : null;
                updated = true;
            }

            // Update status
            if (request.Status.HasValue)
            {
                transaction.Status = request.Status.Value;
                updated = true;
            }

            // Update cleared flag
            if (request.IsCleared.HasValue)
            {
                transaction.IsCleared = request.IsCleared.Value;
                updated = true;
            }

            // Note: IsReconciled flag is intentionally not updated here.
            // Reconciliation must be done through ReconciliationService to maintain proper audit trail.

            if (updated)
            {
                transaction.UpdatedAt = DateTime.UtcNow;
                result.UpdatedCount++;
            }
        }

        if (result.UpdatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);

            // Invalidate caches for all affected transaction dates
            var affectedDates = transactionsToUpdate.Select(t => t.Date).Distinct();
            foreach (var date in affectedDates)
            {
                await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, date);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates and adds <see cref="TransactionSplit"/> entities to the context for a given transaction.
    /// Callers must call <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>
    /// after this method returns.
    /// </summary>
    /// <param name="context">The ambient DbContext (caller-owned).</param>
    /// <param name="transactionId">The owning transaction's database ID.</param>
    /// <param name="splits">Pairs of (split DTO, already-normalized amount).</param>
    private void AddTransactionSplits(ApplicationDbContext context, int transactionId, IEnumerable<(TransactionSplitDto Dto, decimal NormalizedAmount)> splits)
    {
        foreach (var (splitDto, normalizedSplitAmount) in splits)
        {
            context.TransactionSplits.Add(new TransactionSplit
            {
                TransactionId = transactionId,
                CategoryId = splitDto.CategoryId,
                Amount = normalizedSplitAmount,
                Memo = splitDto.Memo,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public SplitValidationResult ValidateSplits(decimal transactionAmount, List<TransactionSplitDto> splits)
    {
        if (splits == null || splits.Count == 0)
        {
            return SplitValidationResult.Failure("At least one split is required");
        }

        var errors = new List<string>();
        var totalSplitAmount = splits.Sum(s => s.Amount);

        // Check if split amounts sum to transaction amount (with small tolerance for rounding)
        if (Math.Abs(totalSplitAmount - transactionAmount) > SplitAmountTolerance)
        {
            errors.Add($"Split amounts ({totalSplitAmount:N2}) must equal transaction amount ({transactionAmount:N2})");
        }

        // Check for zero or invalid amounts
        if (splits.Any(s => s.Amount == 0))
        {
            errors.Add("Split amounts cannot be zero");
        }

        // Check for consistent signs (all splits should have same sign as transaction)
        var transactionSign = Math.Sign(transactionAmount);
        if (splits.Any(s => Math.Sign(s.Amount) != transactionSign))
        {
            errors.Add("All split amounts must have the same sign as the transaction amount");
        }

        return errors.Count > 0 
            ? SplitValidationResult.Failure(errors.ToArray())
            : SplitValidationResult.Success();
    }

    public async Task<Transaction> CreateTransactionWithSplitsAsync(
        string userId,
        int accountId,
        DateTime date,
        decimal amount,
        int? payeeId,
        string? memo,
        TransactionStatus status,
        bool isCleared,
        string? referenceNumber,
        string? tags,
        List<TransactionSplitDto> splits,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(splits);
        // Validate splits
        var validation = ValidateSplits(amount, splits);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid splits: {string.Join(", ", validation.Errors)}");
        }

        // Create transaction without category (splits define categories)
        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = accountId,
            Date = date,
            Amount = amount,
            PayeeId = payeeId,
            CategoryId = null, // Null when using splits
            Memo = memo,
            Status = status,
            IsCleared = isCleared,
            ReferenceNumber = referenceNumber,
            Tags = tags,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Normalize split amounts before opening the DB transaction to keep I/O outside the transaction scope.
        var normalizedSplits = new List<(TransactionSplitDto Dto, decimal NormalizedAmount)>(splits.Count);
        foreach (var splitDto in splits)
        {
            var normalizedSplitAmount = await NormalizeAmountByCategoryTypeAsync(context, splitDto.Amount, splitDto.CategoryId, cancellationToken);
            normalizedSplits.Add((splitDto, normalizedSplitAmount));
        }

        // Wrap both inserts and ledger generation in a single DB transaction so a
        // partial failure never leaves an orphaned transaction or missing splits.
        await using var dbTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken); // needed to obtain transaction.Id for the FK

        // Create splits with normalized amounts based on category type
        AddTransactionSplits(context, transaction.Id, normalizedSplits);

        await context.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        var createdTransaction = await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .FirstAsync(t => t.Id == transaction.Id, cancellationToken);

        // Generate ledger entries for double-entry bookkeeping
        await _ledgerService.GenerateLedgerEntriesAsync(context, createdTransaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        // Cache invalidation must happen after commit so readers always see committed data.
        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, date);

        return createdTransaction;
    }

    public async Task<bool> UpdateTransactionWithSplitsAsync(
        int transactionId,
        string userId,
        DateTime date,
        decimal amount,
        int? payeeId,
        string? memo,
        TransactionStatus status,
        bool isCleared,
        string? referenceNumber,
        string? tags,
        List<TransactionSplitDto> splits,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(splits);
        // Validate splits
        var validation = ValidateSplits(amount, splits);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid splits: {string.Join(", ", validation.Errors)}");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Cannot modify reconciled transactions
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction");

        // Update transaction properties
        transaction.Date = date;
        transaction.Amount = amount;
        transaction.PayeeId = payeeId;
        transaction.CategoryId = null; // Null when using splits
        transaction.Memo = memo;
        transaction.Status = status;
        transaction.IsCleared = isCleared;
        transaction.ReferenceNumber = referenceNumber;
        transaction.Tags = tags;
        transaction.UpdatedAt = DateTime.UtcNow;

        // Remove existing splits
        context.TransactionSplits.RemoveRange(transaction.Splits);

        // Normalize new split amounts then add them via shared helper
        var normalizedSplits = new List<(TransactionSplitDto Dto, decimal NormalizedAmount)>(splits.Count);
        foreach (var splitDto in splits)
        {
            var normalizedAmount = await NormalizeAmountByCategoryTypeAsync(context, splitDto.Amount, splitDto.CategoryId, cancellationToken);
            normalizedSplits.Add((splitDto, normalizedAmount));
        }

        AddTransactionSplits(context, transaction.Id, normalizedSplits);

        await context.SaveChangesAsync(cancellationToken);

        // Load the Account navigation property for ledger entry regeneration
        await context.Entry(transaction)
            .Reference(t => t.Account)
            .LoadAsync(cancellationToken);

        // Reload splits for ledger entry generation
        await context.Entry(transaction)
            .Collection(t => t.Splits)
            .LoadAsync(cancellationToken);

        // Regenerate ledger entries to reflect the updated transaction with splits
        await _ledgerService.RegenerateLedgerEntriesAsync(context, transaction, cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, date);

        return true;
    }

    public async Task<StatusUpdateResult> BulkUpdateStatusAsync(
        string userId,
        StatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new StatusUpdateResult();

        if (request.TransactionIds.Count == 0)
            return result;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Load transactions
        var transactions = await context.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            // Skip reconciled transactions (preserve data integrity)
            if (request.SkipReconciled && transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                continue;
            }

            // Skip transfers if requested
            if (request.SkipTransfers && transaction.TransferTransactionId.HasValue)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                continue;
            }

            // Update status
            transaction.Status = request.NewStatus;
            transaction.UpdatedAt = DateTime.UtcNow;
            result.UpdatedCount++;
        }

        // Set skip reason if any skipped
        if (result.SkippedTransactionIds.Count > 0)
        {
            var reasons = new List<string>();
            if (request.SkipReconciled)
                reasons.Add("reconciled");
            if (request.SkipTransfers)
                reasons.Add("transfers");
            result.SkipReason = string.Join(" or ", reasons);
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<int> GetPendingTransactionCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Transactions
            .CountAsync(t => t.UserId == userId && t.Status == TransactionStatus.Pending, cancellationToken);
    }

    public async Task<StatusUpdateResult> PostAllPendingTransactionsAsync(
        string userId,
        DateTime? throughDate = null,
        int? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var result = new StatusUpdateResult();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Transactions
            .Where(t => t.UserId == userId && t.Status == TransactionStatus.Pending);

        // Filter by date if specified (post pending transactions up to a certain date)
        if (throughDate.HasValue)
            query = query.Where(t => t.Date <= throughDate.Value);

        // Filter by account if specified
        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        var transactions = await query.ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            // Skip reconciled transactions (should not happen for pending, but safety check)
            if (transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                continue;
            }

            transaction.Status = TransactionStatus.Posted;
            transaction.UpdatedAt = DateTime.UtcNow;
            result.UpdatedCount++;
        }

        if (result.SkippedTransactionIds.Count > 0)
        {
            result.SkipReason = "reconciled";
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<FlagUpdateResult> BulkUpdateClearedFlagAsync(
        string userId,
        ClearedFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new FlagUpdateResult();

        if (request.TransactionIds.Count == 0)
            return result;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Load transactions
        var transactions = await context.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            // Always skip reconciled transactions - they are immutable
            if (transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                continue;
            }

            // Update cleared flag
            transaction.IsCleared = request.IsCleared;
            transaction.UpdatedAt = DateTime.UtcNow;
            result.UpdatedCount++;
        }

        if (result.SkippedTransactionIds.Count > 0)
        {
            result.SkipReason = "reconciled";
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<bool> ToggleClearedFlagAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Cannot toggle cleared if reconciled
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify cleared flag on reconciled transaction");

        transaction.IsCleared = !transaction.IsCleared;
        transaction.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

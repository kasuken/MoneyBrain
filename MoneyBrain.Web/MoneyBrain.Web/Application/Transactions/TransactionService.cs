using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    /// Money columns are decimal(18,2); split totals must match the parent exactly at this precision.
    /// </summary>
    private const int MoneyDecimals = 2;

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
    /// Normalizes an amount by the owned category's type: Income categories are positive and
    /// Expense categories are negative. Throws when the category is not owned by the user so a
    /// foreign category can never be referenced or used to infer another user's data.
    /// </summary>
    private static async Task<decimal> NormalizeAmountByCategoryTypeAsync(ApplicationDbContext context, string userId, decimal amount, int? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
            return amount;

        var type = await context.Categories
            .Where(c => c.Id == categoryId.Value && c.UserId == userId)
            .Select(c => (CategoryType?)c.CategoryGroup.Type)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Category not found or does not belong to user");

        var absoluteAmount = Math.Abs(amount);
        return type == CategoryType.Income ? absoluteAmount : -absoluteAmount;
    }

    private static async Task EnsureAccountOwnedAsync(ApplicationDbContext context, string userId, int accountId, CancellationToken cancellationToken)
    {
        if (!await context.Accounts.AnyAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken))
            throw new InvalidOperationException("Account not found or does not belong to user");
    }

    private static async Task EnsurePayeeOwnedAsync(ApplicationDbContext context, string userId, int? payeeId, CancellationToken cancellationToken)
    {
        if (payeeId is int id && !await context.Payees.AnyAsync(p => p.Id == id && p.UserId == userId, cancellationToken))
            throw new InvalidOperationException("Payee not found or does not belong to user");
    }

    /// <summary>
    /// Starts the single database transaction that wraps a mutation's reads, saves and ledger work.
    /// Serializable isolation keeps reconciliation from locking rows between the immutability check
    /// and the save, and a disposed uncommitted transaction rolls back partial financial state.
    /// </summary>
    private static Task<IDbContextTransaction> BeginMutationAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    /// <summary>Transfer legs must change as a linked pair through the transfer service.</summary>
    private static void ThrowIfTransferLeg(Transaction transaction)
    {
        if (transaction.TransferTransactionId.HasValue)
            throw new InvalidOperationException("Transfer transactions must be changed through the transfer editor");
    }

    private static async Task InvalidateCachesAsync(ICacheService cacheService, string userId, IEnumerable<DateTime> dates)
    {
        foreach (var month in dates.Select(d => new DateTime(d.Year, d.Month, 1)).Distinct())
            await TransactionCacheHelper.InvalidateRelatedCachesAsync(cacheService, userId, month);
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
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        await EnsureAccountOwnedAsync(context, userId, accountId, cancellationToken);
        await EnsurePayeeOwnedAsync(context, userId, payeeId, cancellationToken);

        // Normalize amount based on category type (Income = positive, Expense = negative)
        var normalizedAmount = await NormalizeAmountByCategoryTypeAsync(context, userId, amount, categoryId, cancellationToken);

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

        await dbTransaction.CommitAsync(cancellationToken);

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
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        var transaction = await context.Transactions
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction");

        ThrowIfTransferLeg(transaction);
        await EnsurePayeeOwnedAsync(context, userId, payeeId, cancellationToken);

        // Normalize amount based on category type (Income = positive, Expense = negative)
        var normalizedAmount = await NormalizeAmountByCategoryTypeAsync(context, userId, amount, categoryId, cancellationToken);
        var previousDate = transaction.Date;

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

        // A single-category update replaces any split lines; otherwise stale splits would keep
        // driving the ledger and category reports while the transaction shows one category.
        context.TransactionSplits.RemoveRange(transaction.Splits);
        transaction.Splits.Clear();

        await context.SaveChangesAsync(cancellationToken);

        // Load the Account navigation property for ledger entry regeneration
        await context.Entry(transaction)
            .Reference(t => t.Account)
            .LoadAsync(cancellationToken);

        // Regenerate ledger entries to reflect the updated transaction
        await _ledgerService.RegenerateLedgerEntriesAsync(context, transaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        await InvalidateCachesAsync(_cacheService, userId, [previousDate, date]);

        return true;
    }

    public async Task<bool> DeleteTransactionAsync(int transactionId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot delete reconciled transaction");

        // Deleting one leg would leave the other half of the transfer unbalanced.
        ThrowIfTransferLeg(transaction);

        var transactionDate = transaction.Date;

        // Delete ledger entries first (cascade delete would handle this, but explicit is clearer)
        await _ledgerService.DeleteLedgerEntriesAsync(context, transactionId, cancellationToken);

        context.Transactions.Remove(transaction);
        await context.SaveChangesAsync(cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

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
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        if (!request.ClearPayee)
            await EnsurePayeeOwnedAsync(context, userId, request.PayeeId, cancellationToken);

        var changesCategory = request.ClearCategory || request.CategoryId.HasValue;
        CategoryType? newCategoryType = null;
        if (!request.ClearCategory && request.CategoryId is int requestedCategoryId)
        {
            newCategoryType = await context.Categories
                .Where(c => c.Id == requestedCategoryId && c.UserId == userId)
                .Select(c => (CategoryType?)c.CategoryGroup.Type)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Category not found or does not belong to user");
        }

        // Get all transactions to update; account and splits are needed for ledger regeneration.
        var transactions = await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Splits)
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        var skipReasons = new List<string>();
        var updatedTransactions = new List<Transaction>();

        foreach (var transaction in transactions)
        {
            if (transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                AddReason(skipReasons, "Reconciled transactions cannot be modified");
                continue;
            }

            // Transfers carry no category and split lines own their categories, so a bulk
            // category change would break the transfer pair or the split ledger.
            if (changesCategory && (transaction.TransferTransactionId.HasValue || transaction.Splits.Count > 0))
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                AddReason(skipReasons, "Transfers and split transactions must be recategorized individually");
                continue;
            }

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
                // Same sign rule as single edits: Income = positive, Expense = negative.
                var absoluteAmount = Math.Abs(transaction.Amount);
                transaction.Amount = newCategoryType == CategoryType.Income ? absoluteAmount : -absoluteAmount;
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
                updatedTransactions.Add(transaction);
                result.UpdatedCount++;
            }
        }

        if (skipReasons.Count > 0)
        {
            result.SkipReason = string.Join("; ", skipReasons);
        }

        if (result.UpdatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);

            // Ledger entries carry the category and signed amount, so only category changes need
            // regeneration; payee, tags, status and cleared are not part of the ledger.
            if (changesCategory)
            {
                foreach (var transaction in updatedTransactions)
                {
                    await _ledgerService.RegenerateLedgerEntriesAsync(context, transaction, cancellationToken);
                }
            }

            await dbTransaction.CommitAsync(cancellationToken);

            await InvalidateCachesAsync(_cacheService, userId, updatedTransactions.Select(t => t.Date));
        }

        return result;
    }

    private static void AddReason(List<string> reasons, string reason)
    {
        if (!reasons.Contains(reason))
            reasons.Add(reason);
    }

    private static IEnumerable<TransactionSplit> CreateSplitEntities(IEnumerable<TransactionSplitDto> normalizedSplits) =>
        normalizedSplits.Select(split => new TransactionSplit
        {
            CategoryId = split.CategoryId,
            Amount = split.Amount,
            Memo = split.Memo,
            CreatedAt = DateTime.UtcNow
        });

    /// <summary>
    /// Normalizes each split by its owned category's type and then validates the normalized lines,
    /// so sign normalization can never produce splits that no longer add up to the transaction.
    /// </summary>
    private async Task<List<TransactionSplitDto>> NormalizeAndValidateSplitsAsync(
        ApplicationDbContext context,
        string userId,
        decimal transactionAmount,
        List<TransactionSplitDto> splits,
        CancellationToken cancellationToken)
    {
        var normalizedSplits = new List<TransactionSplitDto>(splits.Count);
        foreach (var split in splits)
        {
            ArgumentNullException.ThrowIfNull(split, nameof(splits));
            normalizedSplits.Add(new TransactionSplitDto
            {
                Id = split.Id,
                CategoryId = split.CategoryId,
                Memo = split.Memo,
                Amount = await NormalizeAmountByCategoryTypeAsync(context, userId, split.Amount, split.CategoryId, cancellationToken)
            });
        }

        var validation = ValidateSplits(transactionAmount, normalizedSplits);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid splits: {string.Join(", ", validation.Errors)}");
        }

        return normalizedSplits;
    }

    public SplitValidationResult ValidateSplits(decimal transactionAmount, List<TransactionSplitDto> splits)
    {
        if (splits == null || splits.Count == 0)
        {
            return SplitValidationResult.Failure("At least one split is required");
        }

        var errors = new List<string>();

        if (transactionAmount == 0)
        {
            errors.Add("Transaction amount cannot be zero");
        }

        // Sub-cent values would be rounded by the store and could break the exact-sum invariant
        // after persistence, so amounts are restricted to the column precision.
        if (decimal.Round(transactionAmount, MoneyDecimals) != transactionAmount ||
            splits.Any(s => decimal.Round(s.Amount, MoneyDecimals) != s.Amount))
        {
            errors.Add("Amounts can have at most two decimal places");
        }

        var totalSplitAmount = splits.Sum(s => s.Amount);

        // The split total must equal the transaction amount exactly; no rounding tolerance.
        if (totalSplitAmount != transactionAmount)
        {
            errors.Add($"Split amounts ({totalSplitAmount}) must equal transaction amount ({transactionAmount})");
        }

        // Check for zero or invalid amounts
        if (splits.Any(s => s.Amount == 0))
        {
            errors.Add("Split amounts cannot be zero");
        }

        // Mixed signs are allowed (for example a purchase with a cashback income line): each
        // split produces its own balanced ledger pair, and the total must still match.

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

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Wrap validation, inserts and ledger generation in a single DB transaction so a
        // partial failure never leaves an orphaned transaction or missing splits.
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        await EnsureAccountOwnedAsync(context, userId, accountId, cancellationToken);
        await EnsurePayeeOwnedAsync(context, userId, payeeId, cancellationToken);
        var normalizedSplits = await NormalizeAndValidateSplitsAsync(context, userId, amount, splits, cancellationToken);

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

        foreach (var split in CreateSplitEntities(normalizedSplits))
        {
            transaction.Splits.Add(split);
        }

        context.Transactions.Add(transaction);
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

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        var transaction = await context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Splits)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Cannot modify reconciled transactions
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction");

        ThrowIfTransferLeg(transaction);
        await EnsurePayeeOwnedAsync(context, userId, payeeId, cancellationToken);
        var normalizedSplits = await NormalizeAndValidateSplitsAsync(context, userId, amount, splits, cancellationToken);
        var previousDate = transaction.Date;

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

        // Replace existing splits; the in-memory collection then matches what the ledger must reflect.
        context.TransactionSplits.RemoveRange(transaction.Splits);
        transaction.Splits.Clear();
        foreach (var split in CreateSplitEntities(normalizedSplits))
        {
            transaction.Splits.Add(split);
        }

        await context.SaveChangesAsync(cancellationToken);

        // Regenerate ledger entries to reflect the updated transaction with splits
        await _ledgerService.RegenerateLedgerEntriesAsync(context, transaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        await InvalidateCachesAsync(_cacheService, userId, [previousDate, date]);

        return true;
    }

    public async Task<StatusUpdateResult> BulkUpdateStatusAsync(
        string userId,
        StatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        var result = new StatusUpdateResult();

        if (request.TransactionIds.Count == 0)
            return result;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        // Load transactions
        var transactions = await context.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        var skipReasons = new List<string>();
        var updatedDates = new List<DateTime>();

        foreach (var transaction in transactions)
        {
            // Reconciled transactions are immutable regardless of the request's skip option.
            if (transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                AddReason(skipReasons, "reconciled");
                continue;
            }

            // Skip transfers if requested
            if (request.SkipTransfers && transaction.TransferTransactionId.HasValue)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                AddReason(skipReasons, "transfers");
                continue;
            }

            // Status is not part of ledger entries, so no ledger regeneration is needed.
            transaction.Status = request.NewStatus;
            transaction.UpdatedAt = DateTime.UtcNow;
            updatedDates.Add(transaction.Date);
            result.UpdatedCount++;
        }

        // Set skip reason if any skipped
        if (skipReasons.Count > 0)
        {
            result.SkipReason = string.Join(" or ", skipReasons);
        }

        if (result.UpdatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            // Reports and budgets filter on status, so their cached months are now stale.
            await InvalidateCachesAsync(_cacheService, userId, updatedDates);
        }

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
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var result = new StatusUpdateResult();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        var query = context.Transactions
            .Where(t => t.UserId == userId && t.Status == TransactionStatus.Pending);

        // Filter by date if specified (post pending transactions up to a certain date)
        if (throughDate.HasValue)
            query = query.Where(t => t.Date <= throughDate.Value);

        // Filter by account if specified
        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        var transactions = await query.ToListAsync(cancellationToken);
        var updatedDates = new List<DateTime>();

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
            updatedDates.Add(transaction.Date);
            result.UpdatedCount++;
        }

        if (result.SkippedTransactionIds.Count > 0)
        {
            result.SkipReason = "reconciled";
        }

        if (result.UpdatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            // Reports and budgets filter on status, so their cached months are now stale.
            await InvalidateCachesAsync(_cacheService, userId, updatedDates);
        }

        return result;
    }

    public async Task<FlagUpdateResult> BulkUpdateClearedFlagAsync(
        string userId,
        ClearedFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);
        var result = new FlagUpdateResult();

        if (request.TransactionIds.Count == 0)
            return result;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

        // Load transactions
        var transactions = await context.Transactions
            .Where(t => request.TransactionIds.Contains(t.Id) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        var updatedDates = new List<DateTime>();

        foreach (var transaction in transactions)
        {
            // Always skip reconciled transactions - they are immutable
            if (transaction.IsReconciled)
            {
                result.SkippedTransactionIds.Add(transaction.Id);
                continue;
            }

            // The cleared flag is not part of ledger entries, so no ledger regeneration is needed.
            transaction.IsCleared = request.IsCleared;
            transaction.UpdatedAt = DateTime.UtcNow;
            updatedDates.Add(transaction.Date);
            result.UpdatedCount++;
        }

        if (result.SkippedTransactionIds.Count > 0)
        {
            result.SkipReason = "reconciled";
        }

        if (result.UpdatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            await InvalidateCachesAsync(_cacheService, userId, updatedDates);
        }

        return result;
    }

    public async Task<bool> ToggleClearedFlagAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var dbTransaction = await BeginMutationAsync(context, cancellationToken);

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
        await dbTransaction.CommitAsync(cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, transaction.Date);
        return true;
    }
}

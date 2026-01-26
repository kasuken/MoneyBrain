using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Transactions.Transfers;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions;

public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _context;

    public TransactionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Transaction>> GetAccountTransactionsAsync(int accountId, string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Transactions
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .Where(t => t.AccountId == accountId && t.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .Where(t => t.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date <= endDate.Value);

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int transactionId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
                .ThenInclude(c => c!.CategoryGroup)
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
        CancellationToken cancellationToken = default)
    {
        // Verify account belongs to user
        var accountExists = await _context.Accounts
            .AnyAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (!accountExists)
            throw new InvalidOperationException("Account not found or does not belong to user");

        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = accountId,
            Date = date,
            Amount = amount,
            PayeeId = payeeId,
            CategoryId = categoryId,
            Memo = memo,
            Status = status,
            IsCleared = isCleared,
            ReferenceNumber = referenceNumber,
            Tags = tags
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction");

        transaction.Date = date;
        transaction.Amount = amount;
        transaction.PayeeId = payeeId;
        transaction.CategoryId = categoryId;
        transaction.Memo = memo;
        transaction.Status = status;
        transaction.IsCleared = isCleared;
        transaction.ReferenceNumber = referenceNumber;
        transaction.Tags = tags;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTransactionAsync(int transactionId, string userId, CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        // Enforce reconciled transaction immutability
        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot delete reconciled transaction");

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<Transaction>> SearchTransactionsAsync(string userId, TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Category)
                .ThenInclude(c => c!.CategoryGroup)
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

    public async Task<List<Payee>> GetPayeesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payee> CreateOrGetPayeeAsync(string userId, string name, int? defaultCategoryId = null, CancellationToken cancellationToken = default)
    {
        // Normalize the payee name
        var normalizedName = PayeeNormalizer.Normalize(name);
        var normalizedKey = PayeeNormalizer.GetNormalizedKey(normalizedName);

        // Try to find existing payee by normalized key
        var existingPayees = await _context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync(cancellationToken);

        var matchingPayee = existingPayees.FirstOrDefault(p => 
            PayeeNormalizer.GetNormalizedKey(p.Name) == normalizedKey);

        if (matchingPayee != null)
            return matchingPayee;

        // Create new payee with normalized name
        var payee = new Payee
        {
            UserId = userId,
            Name = normalizedName,
            DefaultCategoryId = defaultCategoryId
        };

        _context.Payees.Add(payee);
        await _context.SaveChangesAsync(cancellationToken);

        return payee;
    }

    public async Task<List<PayeeWithUsage>> GetPayeesWithUsageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var payees = await _context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var payeesWithUsage = new List<PayeeWithUsage>();

        foreach (var payee in payees)
        {
            var transactionCount = await _context.Transactions
                .CountAsync(t => t.PayeeId == payee.Id, cancellationToken);

            var lastTransaction = await _context.Transactions
                .Where(t => t.PayeeId == payee.Id)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync(cancellationToken);

            payeesWithUsage.Add(new PayeeWithUsage
            {
                Payee = payee,
                TransactionCount = transactionCount,
                LastUsedDate = lastTransaction?.Date
            });
        }

        return payeesWithUsage;
    }

    public async Task<List<PayeeDuplicateGroup>> FindDuplicatePayeesAsync(string userId, double similarityThreshold = 0.85, CancellationToken cancellationToken = default)
    {
        var payeesWithUsage = await GetPayeesWithUsageAsync(userId, cancellationToken);

        // Group by normalized key
        var groups = new Dictionary<string, PayeeDuplicateGroup>();

        foreach (var payeeWithUsage in payeesWithUsage)
        {
            var normalizedKey = PayeeNormalizer.GetNormalizedKey(payeeWithUsage.Payee.Name);

            if (!groups.ContainsKey(normalizedKey))
            {
                groups[normalizedKey] = new PayeeDuplicateGroup
                {
                    NormalizedKey = normalizedKey
                };
            }

            groups[normalizedKey].Payees.Add(payeeWithUsage);
        }

        // Return only groups with duplicates
        return groups.Values
            .Where(g => g.HasDuplicates)
            .OrderByDescending(g => g.Payees.Sum(p => p.TransactionCount))
            .ToList();
    }

    public async Task<bool> MergePayeesAsync(string userId, int targetPayeeId, List<int> sourcePayeeIds, CancellationToken cancellationToken = default)
    {
        // Validate target payee
        var targetPayee = await _context.Payees
            .FirstOrDefaultAsync(p => p.Id == targetPayeeId && p.UserId == userId, cancellationToken);

        if (targetPayee == null)
            return false;

        // Validate source payees
        var sourcePayees = await _context.Payees
            .Where(p => sourcePayeeIds.Contains(p.Id) && p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (sourcePayees.Count != sourcePayeeIds.Count)
            return false;

        // Update all transactions from source payees to target payee
        var transactionsToUpdate = await _context.Transactions
            .Where(t => sourcePayeeIds.Contains(t.PayeeId!.Value) && t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactionsToUpdate)
        {
            // Don't modify reconciled transactions
            if (!transaction.IsReconciled)
            {
                transaction.PayeeId = targetPayeeId;
                transaction.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Soft delete source payees
        foreach (var sourcePayee in sourcePayees)
        {
            sourcePayee.IsActive = false;
            sourcePayee.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RenamePayeeAsync(int payeeId, string userId, string newName, CancellationToken cancellationToken = default)
    {
        var payee = await _context.Payees
            .FirstOrDefaultAsync(p => p.Id == payeeId && p.UserId == userId, cancellationToken);

        if (payee == null)
            return false;

        // Normalize the new name
        payee.Name = PayeeNormalizer.Normalize(newName);
        payee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteUnusedPayeesAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Find payees with no transactions
        var allPayees = await _context.Payees
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync(cancellationToken);

        var unusedPayees = new List<Payee>();

        foreach (var payee in allPayees)
        {
            var hasTransactions = await _context.Transactions
                .AnyAsync(t => t.PayeeId == payee.Id, cancellationToken);

            if (!hasTransactions)
            {
                unusedPayees.Add(payee);
            }
        }

        // Soft delete unused payees
        foreach (var payee in unusedPayees)
        {
            payee.IsActive = false;
            payee.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return unusedPayees.Count;
    }

    public async Task<BulkEditResult> BulkUpdateTransactionsAsync(string userId, BulkEditTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var result = new BulkEditResult();

        if (!request.HasUpdates || request.TransactionIds.Count == 0)
        {
            return result;
        }

        // Get all transactions to update
        var transactions = await _context.Transactions
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

            if (updated)
            {
                transaction.UpdatedAt = DateTime.UtcNow;
                result.UpdatedCount++;
            }
        }

        if (result.UpdatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
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
        if (Math.Abs(totalSplitAmount - transactionAmount) > 0.01m)
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

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        // Create splits
        foreach (var splitDto in splits)
        {
            var split = new TransactionSplit
            {
                TransactionId = transaction.Id,
                CategoryId = splitDto.CategoryId,
                Amount = splitDto.Amount,
                Memo = splitDto.Memo,
                CreatedAt = DateTime.UtcNow
            };

            _context.TransactionSplits.Add(split);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Payee)
            .Include(t => t.Splits)
                .ThenInclude(s => s.Category)
            .FirstAsync(t => t.Id == transaction.Id, cancellationToken);
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
        // Validate splits
        var validation = ValidateSplits(amount, splits);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid splits: {string.Join(", ", validation.Errors)}");
        }

        var transaction = await _context.Transactions
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
        _context.TransactionSplits.RemoveRange(transaction.Splits);

        // Add new splits
        foreach (var splitDto in splits)
        {
            var split = new TransactionSplit
            {
                TransactionId = transaction.Id,
                CategoryId = splitDto.CategoryId,
                Amount = splitDto.Amount,
                Memo = splitDto.Memo,
                CreatedAt = DateTime.UtcNow
            };

            _context.TransactionSplits.Add(split);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TransferResult> CreateTransferAsync(
        string userId,
        TransferDto transfer,
        CancellationToken cancellationToken = default)
    {
        // Validate accounts exist and belong to user
        var fromAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == transfer.FromAccountId && a.UserId == userId, cancellationToken);
        var toAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == transfer.ToAccountId && a.UserId == userId, cancellationToken);

        if (fromAccount == null || toAccount == null)
            throw new InvalidOperationException("One or both accounts not found or do not belong to user");

        if (transfer.FromAccountId == transfer.ToAccountId)
            throw new InvalidOperationException("Cannot transfer to the same account");

        if (transfer.Amount <= 0)
            throw new InvalidOperationException("Transfer amount must be positive");

        // Create FROM transaction (negative amount - money leaving account)
        var fromTransaction = new Transaction
        {
            UserId = userId,
            AccountId = transfer.FromAccountId,
            Date = transfer.Date,
            Amount = -Math.Abs(transfer.Amount), // Always negative
            PayeeId = null,
            CategoryId = null, // Transfers have no category
            Memo = transfer.Memo,
            Status = TransactionStatus.Posted,
            IsCleared = transfer.IsCleared,
            ReferenceNumber = transfer.ReferenceNumber,
            Tags = "transfer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(fromTransaction);
        await _context.SaveChangesAsync(cancellationToken);

        // Create TO transaction (positive amount - money entering account)
        var toTransaction = new Transaction
        {
            UserId = userId,
            AccountId = transfer.ToAccountId,
            Date = transfer.Date,
            Amount = Math.Abs(transfer.Amount), // Always positive
            PayeeId = null,
            CategoryId = null, // Transfers have no category
            Memo = transfer.Memo,
            Status = TransactionStatus.Posted,
            IsCleared = transfer.IsCleared,
            ReferenceNumber = transfer.ReferenceNumber,
            Tags = "transfer",
            TransferTransactionId = fromTransaction.Id, // Link to FROM transaction
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(toTransaction);
        await _context.SaveChangesAsync(cancellationToken);

        // Link FROM transaction to TO transaction
        fromTransaction.TransferTransactionId = toTransaction.Id;
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        fromTransaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstAsync(t => t.Id == fromTransaction.Id, cancellationToken);

        toTransaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstAsync(t => t.Id == toTransaction.Id, cancellationToken);

        return new TransferResult
        {
            FromTransaction = fromTransaction,
            ToTransaction = toTransaction,
            Amount = Math.Abs(transfer.Amount),
            Date = transfer.Date,
            Memo = transfer.Memo
        };
    }

    public async Task<bool> UpdateTransferAsync(
        string userId,
        int fromTransactionId,
        TransferDto transfer,
        CancellationToken cancellationToken = default)
    {
        // Get both transactions
        var fromTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == fromTransactionId && t.UserId == userId, cancellationToken);

        if (fromTransaction == null)
            return false;

        if (!fromTransaction.TransferTransactionId.HasValue)
            throw new InvalidOperationException("Transaction is not a transfer");

        var toTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == fromTransaction.TransferTransactionId.Value && t.UserId == userId, cancellationToken);

        if (toTransaction == null)
            throw new InvalidOperationException("Linked transfer transaction not found");

        // Cannot modify reconciled transfers
        if (fromTransaction.IsReconciled || toTransaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transfer");

        // Validate amount
        if (transfer.Amount <= 0)
            throw new InvalidOperationException("Transfer amount must be positive");

        // Update FROM transaction
        fromTransaction.Date = transfer.Date;
        fromTransaction.Amount = -Math.Abs(transfer.Amount);
        fromTransaction.Memo = transfer.Memo;
        fromTransaction.IsCleared = transfer.IsCleared;
        fromTransaction.ReferenceNumber = transfer.ReferenceNumber;
        fromTransaction.UpdatedAt = DateTime.UtcNow;

        // Update TO transaction
        toTransaction.Date = transfer.Date;
        toTransaction.Amount = Math.Abs(transfer.Amount);
        toTransaction.Memo = transfer.Memo;
        toTransaction.IsCleared = transfer.IsCleared;
        toTransaction.ReferenceNumber = transfer.ReferenceNumber;
        toTransaction.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null)
            return false;

        if (!transaction.TransferTransactionId.HasValue)
            throw new InvalidOperationException("Transaction is not a transfer");

        var linkedTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.TransferTransactionId.Value && t.UserId == userId, cancellationToken);

        // Cannot delete reconciled transfers
        if (transaction.IsReconciled || (linkedTransaction?.IsReconciled == true))
            throw new InvalidOperationException("Cannot delete reconciled transfer");

        // Delete both transactions
        _context.Transactions.Remove(transaction);
        if (linkedTransaction != null)
        {
            _context.Transactions.Remove(linkedTransaction);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TransferResult?> GetTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.TransferTransaction)
                .ThenInclude(tt => tt!.Account)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction == null || !transaction.TransferTransactionId.HasValue)
            return null;

        var linkedTransaction = transaction.TransferTransaction!;

        // Determine which is FROM and which is TO based on amount sign
        var fromTransaction = transaction.Amount < 0 ? transaction : linkedTransaction;
        var toTransaction = transaction.Amount > 0 ? transaction : linkedTransaction;

        return new TransferResult
        {
            FromTransaction = fromTransaction,
            ToTransaction = toTransaction,
            Amount = Math.Abs(transaction.Amount),
            Date = transaction.Date,
            Memo = transaction.Memo
        };
    }
}


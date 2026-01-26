using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
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
}

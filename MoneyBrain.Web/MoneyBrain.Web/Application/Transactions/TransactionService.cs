using Microsoft.EntityFrameworkCore;
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
        // Try to find existing payee
        var payee = await _context.Payees
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Name == name, cancellationToken);

        if (payee != null)
            return payee;

        // Create new payee
        payee = new Payee
        {
            UserId = userId,
            Name = name,
            DefaultCategoryId = defaultCategoryId
        };

        _context.Payees.Add(payee);
        await _context.SaveChangesAsync(cancellationToken);

        return payee;
    }
}

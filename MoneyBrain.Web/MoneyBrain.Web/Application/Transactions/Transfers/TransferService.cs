using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions.Transfers;

/// <summary>
/// Manages fund transfers between accounts.
/// Each transfer is stored as two linked <see cref="Transaction"/> records
/// (a negative debit on the source account and a positive credit on the destination account).
/// </summary>
public class TransferService : ITransferService
{
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerService;
    private readonly ICacheService _cacheService;

    public TransferService(
        ApplicationDbContext context,
        ILedgerService ledgerService,
        ICacheService cacheService)
    {
        _context = context;
        _ledgerService = ledgerService;
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<TransferResult> CreateTransferAsync(
        string userId,
        TransferDto transfer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(transfer);

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

        // Wrap all inserts, linking, and ledger generation in a single DB transaction so that
        // a failure at any point never leaves orphaned or half-linked transfer transactions.
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

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
        await _context.SaveChangesAsync(cancellationToken); // needed to obtain fromTransaction.Id for the FK

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
        await _context.SaveChangesAsync(cancellationToken); // gives toTransaction its database ID

        // Now both IDs are known — set the back-link on FROM and persist it.
        fromTransaction.TransferTransactionId = toTransaction.Id;
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        fromTransaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstAsync(t => t.Id == fromTransaction.Id, cancellationToken);

        toTransaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstAsync(t => t.Id == toTransaction.Id, cancellationToken);

        // Generate ledger entries for both sides of the transfer
        await _ledgerService.GenerateLedgerEntriesAsync(fromTransaction, cancellationToken);
        await _ledgerService.GenerateLedgerEntriesAsync(toTransaction, cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        // Cache invalidation must happen after commit so readers always see committed data.
        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, transfer.Date);

        return new TransferResult
        {
            FromTransaction = fromTransaction,
            ToTransaction = toTransaction,
            Amount = Math.Abs(transfer.Amount),
            Date = transfer.Date,
            Memo = transfer.Memo
        };
    }

    /// <inheritdoc />
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

        // Capture the old date before mutation so we can invalidate the old month's cache
        // when the user moves a transfer across calendar months.
        var oldDate = fromTransaction.Date;

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

        // Always invalidate the new date's month; also invalidate the old month when the
        // transfer crossed a month boundary (otherwise stale balances/cashflow remain visible).
        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, oldDate);
        if (oldDate.Year != transfer.Date.Year || oldDate.Month != transfer.Date.Month)
            await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, transfer.Date);

        return true;
    }

    /// <inheritdoc />
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

        var transactionDate = transaction.Date;

        // Delete both transactions
        _context.Transactions.Remove(transaction);
        if (linkedTransaction != null)
        {
            _context.Transactions.Remove(linkedTransaction);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await TransactionCacheHelper.InvalidateRelatedCachesAsync(_cacheService, userId, transactionDate);

        return true;
    }

    /// <inheritdoc />
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

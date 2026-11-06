using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Reconciliation;

public class ReconciliationService(IDbContextFactory<ApplicationDbContext> contextFactory) : IReconciliationService
{
    public async Task<List<Domain.Entities.Reconciliation>> GetReconciliationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Domain.Entities.Reconciliation>> GetReconciliationsForAccountAsync(int accountId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.AccountId == accountId && r.UserId == userId)
            .OrderByDescending(r => r.StatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Domain.Entities.Reconciliation?> GetReconciliationByIdAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Reconciliations
            .Include(r => r.Account)
            .Include(r => r.Transactions)
                .ThenInclude(t => t.Payee)
            .Include(r => r.Transactions)
                .ThenInclude(t => t.Category)
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);
    }

    public async Task<Domain.Entities.Reconciliation> StartReconciliationAsync(int accountId, string userId, DateTime statementDate, decimal statementBalance, string? notes = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Verify account belongs to user
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account == null)
            throw new InvalidOperationException("Account not found or access denied");

        // Calculate opening balance (last reconciled balance or account opening balance)
        var lastReconciliation = await context.Reconciliations
            .Where(r => r.AccountId == accountId && r.UserId == userId && r.IsCompleted)
            .OrderByDescending(r => r.StatementDate)
            .FirstOrDefaultAsync(cancellationToken);

        var openingBalance = lastReconciliation?.ReconciledBalance ?? account.OpeningBalance;

        var reconciliation = new Domain.Entities.Reconciliation
        {
            UserId = userId,
            AccountId = accountId,
            StatementDate = statementDate,
            StatementBalance = statementBalance,
            OpeningBalance = openingBalance,
            ReconciledBalance = openingBalance, // Will be updated as transactions are reconciled
            Difference = statementBalance - openingBalance,
            IsCompleted = false,
            Notes = notes
        };

        context.Reconciliations.Add(reconciliation);
        await context.SaveChangesAsync(cancellationToken);

        return reconciliation;
    }

    public async Task<List<Transaction>> GetUnreconciledTransactionsAsync(int accountId, string userId, DateTime upToDate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Transactions
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .Where(t => t.AccountId == accountId && 
                       t.UserId == userId && 
                       !t.IsReconciled && 
                       t.Date <= upToDate &&
                        t.Status == TransactionStatus.Posted)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(transactionIds);
        if (transactionIds.Count == 0 || transactionIds.Any(id => id <= 0) ||
            transactionIds.Distinct().Count() != transactionIds.Count)
            throw new ArgumentException("Select distinct, positive transaction identifiers.", nameof(transactionIds));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var operation = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null || reconciliation.IsCompleted)
            return false;

        var transactions = await context.Transactions
            .Where(t => transactionIds.Contains(t.Id) && 
                       t.UserId == userId && 
                       t.AccountId == reconciliation.AccountId &&
                       !t.IsReconciled &&
                       t.Status == TransactionStatus.Posted &&
                       t.Date <= reconciliation.StatementDate)
            .ToListAsync(cancellationToken);
        if (transactions.Count != transactionIds.Count)
            throw new InvalidOperationException("The selection contains a missing, pending, future, or already reconciled transaction.");

        foreach (var transaction in transactions)
        {
            transaction.IsReconciled = true;
            transaction.IsCleared = true;
            transaction.ReconciliationId = reconciliationId;
            transaction.UpdatedAt = DateTime.UtcNow;
        }

        // Totals query persisted rows, so save the selection inside the same atomic operation first.
        await context.SaveChangesAsync(cancellationToken);
        await UpdateReconciliationTotalsAsync(context, reconciliation, userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await operation.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnreconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(transactionIds);
        if (transactionIds.Count == 0 || transactionIds.Any(id => id <= 0) ||
            transactionIds.Distinct().Count() != transactionIds.Count)
            throw new ArgumentException("Select distinct, positive transaction identifiers.", nameof(transactionIds));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var operation = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null || reconciliation.IsCompleted)
            return false;

        var transactions = await context.Transactions
            .Where(t => transactionIds.Contains(t.Id) && 
                       t.UserId == userId && 
                       t.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
        if (transactions.Count != transactionIds.Count)
            throw new InvalidOperationException("The selection contains a missing transaction or one outside this reconciliation.");

        foreach (var transaction in transactions)
        {
            transaction.IsReconciled = false;
            transaction.ReconciliationId = null;
            transaction.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        await UpdateReconciliationTotalsAsync(context, reconciliation, userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await operation.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<Domain.Entities.Reconciliation> CompleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var operation = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null)
            throw new InvalidOperationException("Reconciliation not found");

        if (reconciliation.IsCompleted)
            throw new InvalidOperationException("Reconciliation is already completed");

        if (await context.Transactions.AnyAsync(t => t.ReconciliationId == reconciliationId &&
            (t.UserId != userId || t.AccountId != reconciliation.AccountId || !t.IsReconciled ||
             t.Status != TransactionStatus.Posted || t.Date > reconciliation.StatementDate), cancellationToken))
            throw new InvalidOperationException("The reconciliation contains ineligible transactions.");
        await UpdateReconciliationTotalsAsync(context, reconciliation, userId, cancellationToken);
        if (reconciliation.Difference != 0)
            throw new InvalidOperationException("Reconciliation difference must be zero before completion.");

        reconciliation.IsCompleted = true;
        reconciliation.CompletedAt = DateTime.UtcNow;
        reconciliation.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await operation.CommitAsync(cancellationToken);
        return reconciliation;
    }

    public async Task<bool> DeleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var reconciliation = await context.Reconciliations
            .Include(r => r.Transactions)
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null)
            return false;

        if (reconciliation.IsCompleted)
            throw new InvalidOperationException("Cannot delete completed reconciliation");

        // Unreconcile all transactions
        foreach (var transaction in reconciliation.Transactions)
        {
            transaction.IsReconciled = false;
            transaction.ReconciliationId = null;
            transaction.UpdatedAt = DateTime.UtcNow;
        }

        context.Reconciliations.Remove(reconciliation);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<decimal> CalculateReconciledBalanceAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null)
            throw new InvalidOperationException("Reconciliation not found");

        // Get all reconciled transactions for this reconciliation
        var reconciledTransactions = await context.Transactions
            .Where(t => t.ReconciliationId == reconciliationId && t.UserId == userId)
            .SumAsync(t => t.Amount, cancellationToken);

        return reconciliation.OpeningBalance + reconciledTransactions;
    }

    private async Task UpdateReconciliationTotalsAsync(ApplicationDbContext context, Domain.Entities.Reconciliation reconciliation, string userId, CancellationToken cancellationToken)
    {
        var reconciledTransactions = await context.Transactions
            .Where(t => t.ReconciliationId == reconciliation.Id && t.UserId == userId)
            .SumAsync(t => t.Amount, cancellationToken);

        reconciliation.ReconciledBalance = reconciliation.OpeningBalance + reconciledTransactions;
        reconciliation.Difference = reconciliation.StatementBalance - reconciliation.ReconciledBalance;
        reconciliation.UpdatedAt = DateTime.UtcNow;
    }
}

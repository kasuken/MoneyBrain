using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Reconciliation;

public class ReconciliationService(ApplicationDbContext context) : IReconciliationService
{
    public async Task<List<Domain.Entities.Reconciliation>> GetReconciliationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Domain.Entities.Reconciliation>> GetReconciliationsForAccountAsync(int accountId, string userId, CancellationToken cancellationToken = default)
    {
        return await context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.AccountId == accountId && r.UserId == userId)
            .OrderByDescending(r => r.StatementDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Domain.Entities.Reconciliation?> GetReconciliationByIdAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
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
        return await context.Transactions
            .Include(t => t.Payee)
            .Include(t => t.Category)
            .Where(t => t.AccountId == accountId && 
                       t.UserId == userId && 
                       !t.IsReconciled && 
                       t.Date <= upToDate &&
                        t.Status == Domain.Enums.TransactionStatus.Posted)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null || reconciliation.IsCompleted)
            return false;

        var transactions = await context.Transactions
            .Where(t => transactionIds.Contains(t.Id) && 
                       t.UserId == userId && 
                       t.AccountId == reconciliation.AccountId &&
                       !t.IsReconciled)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.IsReconciled = true;
            transaction.ReconciliationId = reconciliationId;
            transaction.UpdatedAt = DateTime.UtcNow;
        }

        // Recalculate reconciled balance
        reconciliation.ReconciledBalance = await CalculateReconciledBalanceAsync(reconciliationId, userId, cancellationToken);
        reconciliation.Difference = reconciliation.StatementBalance - reconciliation.ReconciledBalance;
        reconciliation.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnreconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null || reconciliation.IsCompleted)
            return false;

        var transactions = await context.Transactions
            .Where(t => transactionIds.Contains(t.Id) && 
                       t.UserId == userId && 
                       t.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.IsReconciled = false;
            transaction.ReconciliationId = null;
            transaction.UpdatedAt = DateTime.UtcNow;
        }

        // Recalculate reconciled balance
        reconciliation.ReconciledBalance = await CalculateReconciledBalanceAsync(reconciliationId, userId, cancellationToken);
        reconciliation.Difference = reconciliation.StatementBalance - reconciliation.ReconciledBalance;
        reconciliation.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Domain.Entities.Reconciliation> CompleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null)
            throw new InvalidOperationException("Reconciliation not found");

        if (reconciliation.IsCompleted)
            throw new InvalidOperationException("Reconciliation is already completed");

        reconciliation.IsCompleted = true;
        reconciliation.CompletedAt = DateTime.UtcNow;
        reconciliation.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return reconciliation;
    }

    public async Task<bool> DeleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default)
    {
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
        var reconciliation = await context.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId && r.UserId == userId, cancellationToken);

        if (reconciliation == null)
            return 0m;

        // Get all reconciled transactions for this reconciliation
        var reconciledTransactions = await context.Transactions
            .Where(t => t.ReconciliationId == reconciliationId && t.UserId == userId)
            .SumAsync(t => t.Amount, cancellationToken);

        return reconciliation.OpeningBalance + reconciledTransactions;
    }
}

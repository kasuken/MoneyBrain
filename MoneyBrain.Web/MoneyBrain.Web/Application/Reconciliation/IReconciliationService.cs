using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Reconciliation;

public interface IReconciliationService
{
    /// <summary>
    /// Get all reconciliations for a user
    /// </summary>
    Task<List<Domain.Entities.Reconciliation>> GetReconciliationsAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get reconciliations for a specific account
    /// </summary>
    Task<List<Domain.Entities.Reconciliation>> GetReconciliationsForAccountAsync(int accountId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific reconciliation by ID
    /// </summary>
    Task<Domain.Entities.Reconciliation?> GetReconciliationByIdAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start a new reconciliation session
    /// </summary>
    Task<Domain.Entities.Reconciliation> StartReconciliationAsync(int accountId, string userId, DateTime statementDate, decimal statementBalance, string? notes = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get unreconciled transactions for an account up to a statement date
    /// </summary>
    Task<List<Transaction>> GetUnreconciledTransactionsAsync(int accountId, string userId, DateTime upToDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark transactions as reconciled in a reconciliation session
    /// </summary>
    Task<bool> ReconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unmark transactions from reconciliation (only if reconciliation not completed)
    /// </summary>
    Task<bool> UnreconcileTransactionsAsync(int reconciliationId, string userId, List<int> transactionIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Complete and lock a reconciliation
    /// </summary>
    Task<Domain.Entities.Reconciliation> CompleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a reconciliation (only if not completed)
    /// </summary>
    Task<bool> DeleteReconciliationAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate reconciled balance for a reconciliation
    /// </summary>
    Task<decimal> CalculateReconciledBalanceAsync(int reconciliationId, string userId, CancellationToken cancellationToken = default);
}

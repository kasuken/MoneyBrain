using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Transactions;

/// <summary>
/// Service for managing transactions
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Get transactions for an account
    /// </summary>
    Task<List<Transaction>> GetAccountTransactionsAsync(int accountId, string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get transactions for a user across all accounts
    /// </summary>
    Task<List<Transaction>> GetUserTransactionsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific transaction by ID
    /// </summary>
    Task<Transaction?> GetTransactionByIdAsync(int transactionId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new transaction
    /// </summary>
    Task<Transaction> CreateTransactionAsync(
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
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing transaction
    /// </summary>
    Task<bool> UpdateTransactionAsync(
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
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a transaction
    /// </summary>
    Task<bool> DeleteTransactionAsync(int transactionId, string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search and filter transactions
    /// </summary>
    Task<List<Transaction>> SearchTransactionsAsync(string userId, TransactionFilter filter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get list of payees for autocomplete
    /// </summary>
    Task<List<Payee>> GetPayeesAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create or get existing payee by name
    /// </summary>
    Task<Payee> CreateOrGetPayeeAsync(string userId, string name, int? defaultCategoryId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get payees with usage statistics
    /// </summary>
    Task<List<PayeeNormalization.PayeeWithUsage>> GetPayeesWithUsageAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find potential duplicate payees
    /// </summary>
    Task<List<PayeeNormalization.PayeeDuplicateGroup>> FindDuplicatePayeesAsync(string userId, double similarityThreshold = 0.85, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Merge multiple payees into a target payee
    /// </summary>
    Task<bool> MergePayeesAsync(string userId, int targetPayeeId, List<int> sourcePayeeIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rename a payee
    /// </summary>
    Task<bool> RenamePayeeAsync(int payeeId, string userId, string newName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete unused payees (no transactions)
    /// </summary>
    Task<int> DeleteUnusedPayeesAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Bulk update multiple transactions (category, payee, tags)
    /// Skips reconciled transactions to preserve data integrity
    /// </summary>
    Task<BulkEditResult> BulkUpdateTransactionsAsync(string userId, BulkEditTransactionRequest request, CancellationToken cancellationToken = default);
}

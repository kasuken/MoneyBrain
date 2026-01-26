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
    /// Get list of payees for autocomplete
    /// </summary>
    Task<List<Payee>> GetPayeesAsync(string userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create or get existing payee by name
    /// </summary>
    Task<Payee> CreateOrGetPayeeAsync(string userId, string name, int? defaultCategoryId = null, CancellationToken cancellationToken = default);
}

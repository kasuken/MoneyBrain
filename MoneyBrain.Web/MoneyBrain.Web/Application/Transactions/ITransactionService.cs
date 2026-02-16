using MoneyBrain.Web.Application.Transactions.BulkEdit;
using MoneyBrain.Web.Application.Transactions.Filtering;
using MoneyBrain.Web.Application.Transactions.FlagManagement;
using MoneyBrain.Web.Application.Transactions.Splits;
using MoneyBrain.Web.Application.Transactions.StatusManagement;
using MoneyBrain.Web.Application.Transactions.Transfers;
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
        bool isRecurring = false,
        RecurrenceFrequency? recurrenceFrequency = null,
        DateTime? recurrenceStartDate = null,
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
        bool isRecurring = false,
        RecurrenceFrequency? recurrenceFrequency = null,
        DateTime? recurrenceStartDate = null,
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
    
    /// <summary>
    /// Validate split transactions against the transaction amount
    /// </summary>
    SplitValidationResult ValidateSplits(decimal transactionAmount, List<TransactionSplitDto> splits);
    
    /// <summary>
    /// Create transaction with splits
    /// </summary>
    Task<Transaction> CreateTransactionWithSplitsAsync(
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
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update transaction with splits
    /// </summary>
    Task<bool> UpdateTransactionWithSplitsAsync(
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
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a transfer between two accounts (creates two linked transactions)
    /// </summary>
    Task<TransferResult> CreateTransferAsync(
        string userId,
        TransferDto transfer,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing transfer (updates both linked transactions)
    /// </summary>
    Task<bool> UpdateTransferAsync(
        string userId,
        int fromTransactionId,
        TransferDto transfer,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a transfer (deletes both linked transactions)
    /// </summary>
    Task<bool> DeleteTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get transfer by transaction ID (returns both linked transactions)
    /// </summary>
    Task<TransferResult?> GetTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Bulk update transaction status (Pending → Posted or Posted → Pending)
    /// Skips reconciled transactions to prevent data corruption
    /// </summary>
    Task<StatusUpdateResult> BulkUpdateStatusAsync(
        string userId,
        StatusUpdateRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get count of pending transactions for user
    /// </summary>
    Task<int> GetPendingTransactionCountAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Post all pending transactions (change status to Posted)
    /// Optionally filter by date range or account
    /// </summary>
    Task<StatusUpdateResult> PostAllPendingTransactionsAsync(
        string userId,
        DateTime? throughDate = null,
        int? accountId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Bulk update cleared flag for multiple transactions
    /// Skips reconciled transactions by default to prevent data corruption
    /// </summary>
    Task<FlagUpdateResult> BulkUpdateClearedFlagAsync(
        string userId,
        ClearedFlagRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Toggle cleared flag for a single transaction
    /// Cannot toggle if reconciled
    /// </summary>
    Task<bool> ToggleClearedFlagAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default);
}

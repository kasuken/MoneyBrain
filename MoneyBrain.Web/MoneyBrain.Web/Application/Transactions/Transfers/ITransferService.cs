namespace MoneyBrain.Web.Application.Transactions.Transfers;

/// <summary>
/// Service for managing fund transfers between accounts.
/// Each transfer is represented as two linked <see cref="Domain.Entities.Transaction"/> records.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Create a transfer between two accounts.
    /// Creates two linked transactions (debit and credit) wrapped in a single DB transaction.
    /// </summary>
    Task<TransferResult> CreateTransferAsync(
        string userId,
        TransferDto transfer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update both sides of an existing transfer.
    /// </summary>
    Task<bool> UpdateTransferAsync(
        string userId,
        int fromTransactionId,
        TransferDto transfer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete both sides of a transfer.
    /// </summary>
    Task<bool> DeleteTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get both sides of a transfer by supplying either transaction ID.
    /// Returns <c>null</c> when the transaction is not found or is not a transfer.
    /// </summary>
    Task<TransferResult?> GetTransferAsync(
        string userId,
        int transactionId,
        CancellationToken cancellationToken = default);
}

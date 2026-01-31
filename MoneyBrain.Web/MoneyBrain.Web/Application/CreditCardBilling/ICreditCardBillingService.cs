using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.CreditCardBilling;

/// <summary>
/// Service for handling credit card billing cycle operations.
/// Manages the workflow of marking pending transactions as posted
/// and creating consolidated billing transactions in linked payment accounts.
/// </summary>
public interface ICreditCardBillingService
{
    /// <summary>
    /// Gets all credit card accounts that are due for billing cycle processing today.
    /// A credit card is due when its BillingCycleDay matches today's day
    /// and it hasn't been processed yet this month.
    /// </summary>
    Task<IReadOnlyList<Account>> GetAccountsDueForBillingAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the billing cycle for a specific credit card account.
    /// This operation:
    /// 1. Marks all pending transactions on the credit card as posted
    /// 2. Creates a consolidated billing transaction in the linked payment account
    /// 3. Updates the LastBillingCycleDate on the credit card account
    /// </summary>
    /// <returns>The result of the billing cycle processing</returns>
    Task<BillingCycleResult> ProcessBillingCycleAsync(
        int creditCardAccountId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes all credit card accounts that are due for billing today.
    /// </summary>
    /// <returns>A summary of all billing cycles processed</returns>
    Task<IReadOnlyList<BillingCycleResult>> ProcessAllDueBillingCyclesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preview of what would happen if billing cycle was processed now.
    /// Useful for displaying to users before manual processing.
    /// </summary>
    Task<BillingCyclePreview> GetBillingCyclePreviewAsync(
        int creditCardAccountId,
        string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of processing a billing cycle for a single credit card.
/// </summary>
public record BillingCycleResult
{
    public required int CreditCardAccountId { get; init; }
    public required string CreditCardAccountName { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TransactionsPosted { get; init; }
    public decimal TotalBilledAmount { get; init; }
    public int? BillingTransactionId { get; init; }
    public string BillingCycleMonth { get; init; } = string.Empty;
}

/// <summary>
/// Preview of a billing cycle before it is processed.
/// </summary>
public record BillingCyclePreview
{
    public required int CreditCardAccountId { get; init; }
    public required string CreditCardAccountName { get; init; }
    public int? LinkedPaymentAccountId { get; init; }
    public string? LinkedPaymentAccountName { get; init; }
    public int PendingTransactionCount { get; init; }
    public decimal TotalPendingAmount { get; init; }
    public IReadOnlyList<Transaction> PendingTransactions { get; init; } = [];
    public bool CanProcess { get; init; }
    public string? ValidationMessage { get; init; }
}

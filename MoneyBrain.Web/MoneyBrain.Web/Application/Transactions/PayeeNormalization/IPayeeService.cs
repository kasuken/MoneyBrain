using MoneyBrain.Web.Application.Transactions.PayeeNormalization;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Transactions;

/// <summary>
/// Service for managing payees: lookup, creation, merging, renaming, and clean-up.
/// </summary>
public interface IPayeeService
{
    /// <summary>
    /// Get all active payees for a user.
    /// </summary>
    Task<List<Payee>> GetPayeesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get payees with usage statistics (transaction count and last-used date).
    /// </summary>
    Task<List<PayeeWithUsage>> GetPayeesWithUsageAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find an existing payee whose normalised key matches <paramref name="name"/>,
    /// or create a new payee when none is found.
    /// </summary>
    Task<Payee> CreateOrGetPayeeAsync(string userId, string name, int? defaultCategoryId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find groups of payees that are likely duplicates based on name similarity.
    /// </summary>
    Task<List<PayeeDuplicateGroup>> FindDuplicatePayeesAsync(string userId, double similarityThreshold = 0.85, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge one or more source payees into a target payee.
    /// Transactions are re-pointed to the target; source payees are soft-deleted.
    /// Reconciled transactions are left untouched.
    /// </summary>
    Task<bool> MergePayeesAsync(string userId, int targetPayeeId, List<int> sourcePayeeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rename a payee, normalising the new name.
    /// </summary>
    Task<bool> RenamePayeeAsync(int payeeId, string userId, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete all active payees that have no associated transactions.
    /// Returns the number of payees deleted.
    /// </summary>
    Task<int> DeleteUnusedPayeesAsync(string userId, CancellationToken cancellationToken = default);
}

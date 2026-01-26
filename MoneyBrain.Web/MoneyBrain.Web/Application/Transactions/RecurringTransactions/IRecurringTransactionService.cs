namespace MoneyBrain.Web.Application.Transactions.RecurringTransactions;

/// <summary>
/// Service for managing recurring (repeating) transactions.
/// </summary>
public interface IRecurringTransactionService
{
    /// <summary>
    /// Calculate the next recurrence date based on frequency.
    /// </summary>
    DateTime CalculateNextRecurrenceDate(DateTime currentDate, Domain.Enums.RecurrenceFrequency frequency);
    
    /// <summary>
    /// Generate all due recurring transactions for a user up to a specific date.
    /// </summary>
    Task<int> GenerateDueRecurringTransactionsAsync(string userId, DateTime upToDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all recurring transaction templates for a user.
    /// </summary>
    Task<List<Domain.Entities.Transaction>> GetRecurringTemplatesAsync(string userId, CancellationToken cancellationToken = default);
}

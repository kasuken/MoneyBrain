namespace MoneyBrain.Web.Application.Settings;

/// <summary>
/// Service for destructive user-data operations: erasing all data and loading demo data.
/// </summary>
public interface IUserDataService
{
    /// <summary>
    /// Erases all data associated with a user, including accounts, transactions, budgets, etc.
    /// Does NOT delete the user identity itself.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task EraseAllUserDataAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads realistic demo data for a user to explore the application.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task LoadDemoDataAsync(string userId, CancellationToken cancellationToken = default);
}

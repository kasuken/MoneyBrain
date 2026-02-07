using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Settings;

/// <summary>
/// Service interface for managing user settings (currency, timezone, etc.).
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Gets user settings for the specified user, or null if not yet configured.
    /// </summary>
    Task<UserSettings?> GetSettingsAsync(string userId);

    /// <summary>
    /// Checks if the user has completed the mandatory settings setup.
    /// </summary>
    Task<bool> HasCompletedSetupAsync(string userId);

    /// <summary>
    /// Creates settings for a new user or updates existing settings.
    /// Marks setup as completed.
    /// </summary>
    Task<UserSettings> SaveSettingsAsync(
        string userId,
        string currencyCode,
        string timeZoneId,
        string? dateFormat = null);

    /// <summary>
    /// Updates tips and insights preferences for a user.
    /// </summary>
    Task UpdateTipsPreferencesAsync(
        string userId,
        bool showTipsAndInsights,
        bool showEducationalTips,
        bool showSpendingInsights,
        bool showBehavioralInsights);

    /// <summary>
    /// Gets a list of commonly used currencies with display names.
    /// </summary>
    IReadOnlyList<CurrencyInfo> GetAvailableCurrencies();

    /// <summary>
    /// Gets a list of available timezones.
    /// </summary>
    IReadOnlyList<TimeZoneInfo> GetAvailableTimeZones();

    /// <summary>
    /// Erases all data associated with a user, including accounts, transactions, budgets, etc.
    /// Does NOT delete the user identity itself.
    /// </summary>
    Task EraseAllUserDataAsync(string userId);

    /// <summary>
    /// Loads realistic demo data for a user to explore the application.
    /// </summary>
    Task LoadDemoDataAsync(string userId);
}

/// <summary>
/// Currency information for display in UI.
/// </summary>
public record CurrencyInfo(string Code, string Name, string Symbol);

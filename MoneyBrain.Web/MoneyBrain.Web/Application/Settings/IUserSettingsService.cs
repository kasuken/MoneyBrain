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
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<UserSettings?> GetSettingsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user has completed the mandatory settings setup.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<bool> HasCompletedSetupAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates settings for a new user or updates existing settings.
    /// Marks setup as completed.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="currencyCode">The ISO currency code to store.</param>
    /// <param name="timeZoneId">The time zone identifier to store.</param>
    /// <param name="dateFormat">Optional date format; defaults to yyyy-MM-dd.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<UserSettings> SaveSettingsAsync(
        string userId,
        string currencyCode,
        string timeZoneId,
        string? dateFormat = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates tips and insights preferences for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="showTipsAndInsights">Whether tips and insights are shown at all.</param>
    /// <param name="showEducationalTips">Whether educational tips are shown.</param>
    /// <param name="showSpendingInsights">Whether spending insights are shown.</param>
    /// <param name="showBehavioralInsights">Whether behavioral insights are shown.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpdateTipsPreferencesAsync(
        string userId,
        bool showTipsAndInsights,
        bool showEducationalTips,
        bool showSpendingInsights,
        bool showBehavioralInsights,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of commonly used currencies with display names.
    /// </summary>
    IReadOnlyList<CurrencyInfo> GetAvailableCurrencies();

    /// <summary>
    /// Gets a list of available timezones.
    /// </summary>
    IReadOnlyList<TimeZoneInfo> GetAvailableTimeZones();
}

/// <summary>
/// Currency information for display in UI.
/// </summary>
public record CurrencyInfo(string Code, string Name, string Symbol);

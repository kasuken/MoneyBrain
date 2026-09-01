using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Settings;

/// <summary>
/// Service implementation for managing user settings.
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ICacheService _cacheService;

    // Common currencies - most widely used first
    private static readonly List<CurrencyInfo> CommonCurrencies =
    [
        new("USD", "US Dollar", "$"),
        new("EUR", "Euro", "€"),
        new("GBP", "British Pound", "£"),
        new("JPY", "Japanese Yen", "¥"),
        new("CHF", "Swiss Franc", "CHF"),
        new("CAD", "Canadian Dollar", "CA$"),
        new("AUD", "Australian Dollar", "A$"),
        new("CNY", "Chinese Yuan", "¥"),
        new("INR", "Indian Rupee", "₹"),
        new("MXN", "Mexican Peso", "$"),
        new("BRL", "Brazilian Real", "R$"),
        new("KRW", "South Korean Won", "₩"),
        new("SGD", "Singapore Dollar", "S$"),
        new("HKD", "Hong Kong Dollar", "HK$"),
        new("NOK", "Norwegian Krone", "kr"),
        new("SEK", "Swedish Krona", "kr"),
        new("DKK", "Danish Krone", "kr"),
        new("PLN", "Polish Zloty", "zł"),
        new("CZK", "Czech Koruna", "Kč"),
        new("HUF", "Hungarian Forint", "Ft"),
        new("TRY", "Turkish Lira", "₺"),
        new("RUB", "Russian Ruble", "₽"),
        new("ZAR", "South African Rand", "R"),
        new("NZD", "New Zealand Dollar", "NZ$"),
        new("ILS", "Israeli Shekel", "₪"),
        new("AED", "UAE Dirham", "د.إ"),
        new("SAR", "Saudi Riyal", "﷼"),
        new("THB", "Thai Baht", "฿"),
        new("PHP", "Philippine Peso", "₱"),
        new("MYR", "Malaysian Ringgit", "RM")
    ];

    public UserSettingsService(IDbContextFactory<ApplicationDbContext> contextFactory, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<UserSettings?> GetSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var cacheKey = CacheKeyHelper.ForUserSettings(userId);
        var cached = await _cacheService.GetAsync<UserSettings>(cacheKey);
        if (cached != null)
            return cached;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var result = await context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        if (result != null)
        {
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> HasCompletedSetupAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.UserSettings
            .AsNoTracking()
            .AnyAsync(us => us.UserId == userId && us.SetupCompleted, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserSettings> SaveSettingsAsync(
        string userId,
        string currencyCode,
        string timeZoneId,
        string? dateFormat = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.UserSettings
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        if (existing != null)
        {
            existing.CurrencyCode = currencyCode;
            existing.TimeZoneId = timeZoneId;
            existing.DateFormat = dateFormat ?? "yyyy-MM-dd";
            existing.SetupCompleted = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new UserSettings
            {
                UserId = userId,
                CurrencyCode = currencyCode,
                TimeZoneId = timeZoneId,
                DateFormat = dateFormat ?? "yyyy-MM-dd",
                SetupCompleted = true,
                CreatedAt = DateTime.UtcNow
            };
            context.UserSettings.Add(existing);
        }

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserSettings(userId));
        return existing;
    }

    /// <inheritdoc />
    public async Task UpdateTipsPreferencesAsync(
        string userId,
        bool showTipsAndInsights,
        bool showEducationalTips,
        bool showSpendingInsights,
        bool showBehavioralInsights,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.UserSettings
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        if (existing == null)
        {
            throw new InvalidOperationException("User settings not found. Please complete setup first.");
        }

        existing.ShowTipsAndInsights = showTipsAndInsights;
        existing.ShowEducationalTips = showEducationalTips;
        existing.ShowSpendingInsights = showSpendingInsights;
        existing.ShowBehavioralInsights = showBehavioralInsights;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveAsync(CacheKeyHelper.ForUserSettings(userId));
    }

    /// <inheritdoc />
    public IReadOnlyList<CurrencyInfo> GetAvailableCurrencies()
    {
        return CommonCurrencies;
    }

    /// <inheritdoc />
    public IReadOnlyList<TimeZoneInfo> GetAvailableTimeZones()
    {
        return TimeZoneInfo.GetSystemTimeZones().ToList();
    }

}

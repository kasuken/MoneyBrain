using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.Settings;

/// <summary>
/// Service implementation for managing user settings.
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private readonly ApplicationDbContext _context;

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

    public UserSettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<UserSettings?> GetSettingsAsync(string userId)
    {
        return await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(us => us.UserId == userId);
    }

    /// <inheritdoc />
    public async Task<bool> HasCompletedSetupAsync(string userId)
    {
        return await _context.UserSettings
            .AsNoTracking()
            .AnyAsync(us => us.UserId == userId && us.SetupCompleted);
    }

    /// <inheritdoc />
    public async Task<UserSettings> SaveSettingsAsync(
        string userId,
        string currencyCode,
        string timeZoneId,
        string? dateFormat = null)
    {
        var existing = await _context.UserSettings
            .FirstOrDefaultAsync(us => us.UserId == userId);

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
            _context.UserSettings.Add(existing);
        }

        await _context.SaveChangesAsync();
        return existing;
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

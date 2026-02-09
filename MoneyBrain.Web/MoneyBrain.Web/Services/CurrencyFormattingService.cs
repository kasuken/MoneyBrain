using MoneyBrain.Web.Application.Settings;

namespace MoneyBrain.Web.Services;

/// <summary>
/// Service implementation for formatting currency amounts with the appropriate currency symbol.
/// </summary>
public class CurrencyFormattingService : ICurrencyFormattingService
{
    private readonly IUserSettingsService _userSettingsService;

    // Currency symbol lookup - maps currency code to symbol
    private readonly Dictionary<string, string> _currencySymbols;

    public CurrencyFormattingService(IUserSettingsService userSettingsService)
    {
        _userSettingsService = userSettingsService;
        
        // Build currency symbol dictionary from available currencies
        _currencySymbols = _userSettingsService.GetAvailableCurrencies()
            .ToDictionary(c => c.Code, c => c.Symbol, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string FormatCurrency(decimal amount, string? currencyCode)
    {
        // Default to USD if currency code is null or empty
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            currencyCode = "USD";
        }

        // Get currency symbol, default to currency code if not found
        var symbol = _currencySymbols.TryGetValue(currencyCode, out var foundSymbol) 
            ? foundSymbol 
            : currencyCode;

        // Format with 2 decimal places
        return $"{symbol}{amount:N2}";
    }
}

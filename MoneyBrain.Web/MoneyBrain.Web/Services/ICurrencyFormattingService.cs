namespace MoneyBrain.Web.Services;

/// <summary>
/// Service interface for formatting currency amounts with the appropriate currency symbol.
/// </summary>
public interface ICurrencyFormattingService
{
    /// <summary>
    /// Formats a decimal amount with the specified currency code and symbol.
    /// </summary>
    /// <param name="amount">The amount to format.</param>
    /// <param name="currencyCode">The ISO currency code (e.g., "USD", "EUR"). Defaults to "USD" if null or empty.</param>
    /// <returns>Formatted currency string with symbol and 2 decimal places.</returns>
    string FormatCurrency(decimal amount, string? currencyCode);
}

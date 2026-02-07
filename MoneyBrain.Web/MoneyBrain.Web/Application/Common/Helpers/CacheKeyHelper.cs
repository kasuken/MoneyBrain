namespace MoneyBrain.Web.Application.Common.Helpers;

/// <summary>
/// Helper class for generating consistent cache keys across the application.
/// </summary>
public static class CacheKeyHelper
{
    /// <summary>
    /// Generates a cache key for user categories.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Cache key in format "user:{userId}:categories"</returns>
    public static string ForUserCategories(string userId) => $"user:{userId}:categories";

    /// <summary>
    /// Generates a cache key for user accounts.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Cache key in format "user:{userId}:accounts"</returns>
    public static string ForUserAccounts(string userId) => $"user:{userId}:accounts";

    /// <summary>
    /// Generates a cache key for user settings.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Cache key in format "user:{userId}:settings"</returns>
    public static string ForUserSettings(string userId) => $"user:{userId}:settings";

    /// <summary>
    /// Generates a cache key for monthly cashflow data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <returns>Cache key in format "user:{userId}:cashflow:{year}:{month}"</returns>
    public static string ForMonthCashflow(string userId, int year, int month) =>
        $"user:{userId}:cashflow:{year}:{month}";

    /// <summary>
    /// Generates a cache key for net worth snapshot data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="date">The snapshot date.</param>
    /// <returns>Cache key in format "user:{userId}:networth:{date:yyyy-MM-dd}"</returns>
    public static string ForNetWorthSnapshot(string userId, DateTime date) =>
        $"user:{userId}:networth:{date:yyyy-MM-dd}";

    /// <summary>
    /// Generates a cache key for budget comparison data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <returns>Cache key in format "user:{userId}:budgetcomparison:{year}:{month}"</returns>
    public static string ForBudgetComparison(string userId, int year, int month) =>
        $"user:{userId}:budgetcomparison:{year}:{month}";

    /// <summary>
    /// Generates a cache key for educational tips.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Cache key in format "user:{userId}:educationaltips"</returns>
    public static string ForEducationalTips(string userId) => $"user:{userId}:educationaltips";

    /// <summary>
    /// Generates a cache key for tip preferences.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Cache key in format "user:{userId}:tippreferences"</returns>
    public static string ForTipPreferences(string userId) => $"user:{userId}:tippreferences";

    /// <summary>
    /// Generates a cache key for spending insights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <returns>Cache key in format "user:{userId}:spendinginsight:{year}:{month}"</returns>
    public static string ForSpendingInsight(string userId, int year, int month) =>
        $"user:{userId}:spendinginsight:{year}:{month}";

    /// <summary>
    /// Generates a cache key for comparative spending insights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <returns>Cache key in format "user:{userId}:comparativespendinginsight:{year}:{month}"</returns>
    public static string ForComparativeSpendingInsight(string userId, int year, int month) =>
        $"user:{userId}:comparativespendinginsight:{year}:{month}";

    /// <summary>
    /// Generates a cache key for budget insights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <returns>Cache key in format "user:{userId}:budgetinsight:{year}:{month}"</returns>
    public static string ForBudgetInsight(string userId, int year, int month) =>
        $"user:{userId}:budgetinsight:{year}:{month}";

    /// <summary>
    /// Generates a cache key for net worth insights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="asOfDate">The date for the insight.</param>
    /// <returns>Cache key in format "user:{userId}:networthinsight:{date:yyyy-MM-dd}"</returns>
    public static string ForNetWorthInsight(string userId, DateTime asOfDate) =>
        $"user:{userId}:networthinsight:{asOfDate:yyyy-MM-dd}";

    /// <summary>
    /// Generates a cache key for net worth trend data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="months">Number of months in the trend.</param>
    /// <returns>Cache key in format "user:{userId}:networthtrend:{months}"</returns>
    public static string ForNetWorthTrend(string userId, int months) =>
        $"user:{userId}:networthtrend:{months}";

    /// <summary>
    /// Generates a cache key for behavior insights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="startDate">The start date of the analysis period.</param>
    /// <param name="endDate">The end date of the analysis period.</param>
    /// <returns>Cache key in format "user:{userId}:behaviorinsights:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}"</returns>
    public static string ForBehaviorInsights(string userId, DateTime startDate, DateTime endDate) =>
        $"user:{userId}:behaviorinsights:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}";

    /// <summary>
    /// Generates a pattern to invalidate all cache entries for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Pattern in format "user:{userId}:*"</returns>
    public static string ForUserPattern(string userId) => $"user:{userId}:*";
}

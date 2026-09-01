using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Application.Transactions;

/// <summary>
/// Shared helper that invalidates the month-level caches touched by any transaction mutation.
/// Extracted to avoid duplicating cache-key logic across <see cref="TransactionService"/> and
/// <see cref="Transfers.TransferService"/>.
/// </summary>
internal static class TransactionCacheHelper
{
    /// <summary>
    /// Removes the cashflow and budget-comparison cache entries for the month that contains
    /// <paramref name="transactionDate"/>.
    /// </summary>
    internal static async Task InvalidateRelatedCachesAsync(
        ICacheService cacheService,
        string userId,
        DateTime transactionDate)
    {
        var cacheKey = CacheKeyHelper.ForMonthCashflow(userId, transactionDate.Year, transactionDate.Month);
        await cacheService.RemoveAsync(cacheKey);

        var budgetComparisonKey = CacheKeyHelper.ForBudgetComparison(userId, transactionDate.Year, transactionDate.Month);
        await cacheService.RemoveAsync(budgetComparisonKey);

        await cacheService.RemoveByPatternAsync($"user:{userId}:networth:*");
    }
}

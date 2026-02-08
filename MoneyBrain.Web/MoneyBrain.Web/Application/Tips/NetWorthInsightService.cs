using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Reporting.NetWorth;
using MoneyBrain.Web.Application.Tips.DTOs;

namespace MoneyBrain.Web.Application.Tips;

/// <summary>
/// Service implementation for generating net worth trend insights.
/// </summary>
public class NetWorthInsightService : INetWorthInsightService
{
    private readonly ICacheService _cacheService;
    private readonly INetWorthService _netWorthService;

    public NetWorthInsightService(
        ICacheService cacheService,
        INetWorthService netWorthService)
    {
        _cacheService = cacheService;
        _netWorthService = netWorthService;
    }

    /// <inheritdoc />
    public async Task<NetWorthInsightDto> GetNetWorthInsightAsync(
        string userId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForNetWorthInsight(userId, asOfDate);
        var cached = await _cacheService.GetAsync<NetWorthInsightDto>(cacheKey);
        if (cached != null)
            return cached;

        var currentSnapshot = await _netWorthService.GetNetWorthSnapshotAsync(
            userId,
            asOfDate,
            cancellationToken);

        // Get previous month for comparison
        var previousDate = asOfDate.AddMonths(-1);
        var previousSnapshot = await _netWorthService.GetNetWorthSnapshotAsync(
            userId,
            previousDate,
            cancellationToken);

        // Get trend data for last 6 months
        var trendData = await GetNetWorthTrendAsync(userId, 6, cancellationToken);

        var insight = new NetWorthInsightDto
        {
            Message = GenerateNetWorthMessage(currentSnapshot.NetWorth, previousSnapshot.NetWorth),
            CurrentNetWorth = currentSnapshot.NetWorth,
            PreviousNetWorth = previousSnapshot.NetWorth,
            TotalAssets = currentSnapshot.TotalAssets,
            TotalLiabilities = currentSnapshot.TotalLiabilities,
            TrendData = trendData,
            GeneratedAt = DateTime.UtcNow
        };

        await _cacheService.SetAsync(cacheKey, insight, TimeSpan.FromHours(1));
        return insight;
    }

    /// <inheritdoc />
    public async Task<NetWorthInsightDto> GetCurrentNetWorthInsightAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await GetNetWorthInsightAsync(userId, DateTime.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<NetWorthTrendDto>> GetNetWorthTrendAsync(
        string userId,
        int months,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForNetWorthTrend(userId, months);
        var cached = await _cacheService.GetAsync<List<NetWorthTrendDto>>(cacheKey);
        if (cached != null)
            return cached;

        var trendData = new List<NetWorthTrendDto>();
        var currentDate = DateTime.UtcNow;

        for (int i = 0; i < months; i++)
        {
            var snapshotDate = currentDate.AddMonths(-i);
            var snapshot = await _netWorthService.GetNetWorthSnapshotAsync(
                userId,
                snapshotDate,
                cancellationToken);

            trendData.Add(new NetWorthTrendDto
            {
                Date = snapshotDate,
                NetWorth = snapshot.NetWorth,
                Assets = snapshot.TotalAssets,
                Liabilities = snapshot.TotalLiabilities
            });
        }

        // Reverse to show oldest first
        trendData.Reverse();

        await _cacheService.SetAsync(cacheKey, trendData, TimeSpan.FromHours(1));
        return trendData;
    }

    private static string GenerateNetWorthMessage(decimal current, decimal previous)
    {
        if (current == previous)
            return $"Net worth is {current:C}, unchanged from the previous period.";

        var change = current - previous;
        var percentChange = previous != 0 ? (change / Math.Abs(previous)) * 100 : 0;
        var direction = change > 0 ? "increased" : "decreased";

        if (Math.Abs(percentChange) < 0.01m)
            return $"Net worth is {current:C}, relatively stable compared to the previous period.";

        return $"Net worth {direction} by {Math.Abs(percentChange):F1}% to {current:C}. " +
               $"This represents a change of {Math.Abs(change):C} from the previous period.";
    }
}

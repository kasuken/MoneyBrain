using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common.Helpers;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Reporting.NetWorth;

/// <summary>
/// Service for calculating net worth (assets - liabilities) over time using double-entry bookkeeping.
/// </summary>
public class NetWorthService : INetWorthService
{
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerService;
    private readonly ICacheService _cacheService;

    public NetWorthService(ApplicationDbContext context, ILedgerService ledgerService, ICacheService cacheService)
    {
        _context = context;
        _ledgerService = ledgerService;
        _cacheService = cacheService;
    }

    /// <inheritdoc />
    public async Task<NetWorthHistoryDto> GetNetWorthHistoryAsync(
        string userId,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new List<NetWorthSnapshotDto>();

        async Task AddSnapshotAsync(DateTime snapshotDate)
        {
            var snapshot = await GetNetWorthSnapshotAsync(userId, snapshotDate, cancellationToken);
            snapshots.Add(snapshot);
        }

        // Generate snapshots at regular intervals
        var currentDate = startDate;
        while (currentDate <= endDate)
        {
            await AddSnapshotAsync(currentDate);
            currentDate = currentDate.AddDays(intervalDays);
        }

        // Always include the end date if not already included
        if (snapshots.Count == 0 || snapshots[^1].Date != endDate)
        {
            await AddSnapshotAsync(endDate);
        }

        // Calculate changes between snapshots
        for (int i = 1; i < snapshots.Count; i++)
        {
            var current = snapshots[i];
            var previous = snapshots[i - 1];

            current.ChangeFromPrevious = current.NetWorth - previous.NetWorth;
            current.PercentageChange = previous.NetWorth != 0
                ? (current.ChangeFromPrevious.Value / Math.Abs(previous.NetWorth) * 100)
                : 0;
        }

        // Build summary
        var firstSnapshot = snapshots.First();
        var lastSnapshot = snapshots.Last();

        return new NetWorthHistoryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            CurrentNetWorth = lastSnapshot.NetWorth,
            CurrentTotalAssets = lastSnapshot.TotalAssets,
            CurrentTotalLiabilities = lastSnapshot.TotalLiabilities,
            StartingNetWorth = firstSnapshot.NetWorth,
            Snapshots = snapshots
        };
    }

    /// <inheritdoc />
    public async Task<NetWorthSnapshotDto> GetNetWorthSnapshotAsync(
        string userId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyHelper.ForNetWorthSnapshot(userId, asOfDate);
        var cached = await _cacheService.GetAsync<NetWorthSnapshotDto>(cacheKey);
        if (cached != null)
            return cached;

        // Get all user accounts
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var accountBalances = new List<AccountBalanceDto>();
        decimal totalAssets = 0;
        decimal totalLiabilities = 0;

        // Calculate balance for each account at the specified date
        foreach (var account in accounts)
        {
            var balance = await _ledgerService.GetAccountBalanceAsync(
                account.Id,
                userId,
                asOfDate,
                cancellationToken);

            var accountBalance = new AccountBalanceDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                AccountType = account.Type.ToString(),
                Balance = balance,
                IsActive = account.IsActive
            };

            accountBalances.Add(accountBalance);

            // Sum assets and liabilities
            // For liabilities, the balance is typically negative in accounting,
            // but we store the absolute value for display purposes
            if (account.Type == AccountType.Asset)
            {
                totalAssets += balance;
            }
            else if (account.Type == AccountType.Liability)
            {
                // Liability balances in accounting are negative (we owe money)
                // For net worth, we want to subtract liabilities from assets
                totalLiabilities += Math.Abs(balance);
            }
        }

        var result = new NetWorthSnapshotDto
        {
            Date = asOfDate,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            AccountBalances = accountBalances
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

        return result;
    }
}

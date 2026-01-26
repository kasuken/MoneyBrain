using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Transactions.Ledger;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Reporting.AccountBalanceHistory;

/// <summary>
/// Service for tracking and analyzing account balance changes over time using double-entry bookkeeping.
/// </summary>
public class AccountBalanceHistoryService : IAccountBalanceHistoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerService;

    public AccountBalanceHistoryService(ApplicationDbContext context, ILedgerService ledgerService)
    {
        _context = context;
        _ledgerService = ledgerService;
    }

    /// <inheritdoc />
    public async Task<AccountBalanceHistoryDto> GetAccountBalanceHistoryAsync(
        string userId,
        int accountId,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        CancellationToken cancellationToken = default)
    {
        // Get account details
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException($"Account {accountId} not found for user {userId}.");
        }

        // Generate snapshots at regular intervals
        var snapshots = new List<BalanceSnapshotDto>();
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            var balance = await _ledgerService.GetAccountBalanceAsync(
                accountId,
                userId,
                currentDate,
                cancellationToken);

            snapshots.Add(new BalanceSnapshotDto
            {
                Date = currentDate,
                Balance = balance
            });

            currentDate = currentDate.AddDays(intervalDays);
        }

        // Always include the end date if not already included
        if (snapshots.Count == 0 || snapshots[^1].Date != endDate)
        {
            var endBalance = await _ledgerService.GetAccountBalanceAsync(
                accountId,
                userId,
                endDate,
                cancellationToken);

            snapshots.Add(new BalanceSnapshotDto
            {
                Date = endDate,
                Balance = endBalance
            });
        }

        // Calculate changes between snapshots
        for (int i = 1; i < snapshots.Count; i++)
        {
            var current = snapshots[i];
            var previous = snapshots[i - 1];

            current.ChangeFromPrevious = current.Balance - previous.Balance;
            current.PercentageChangeFromPrevious = previous.Balance != 0
                ? (current.ChangeFromPrevious.Value / Math.Abs(previous.Balance) * 100)
                : 0;
        }

        // Build result
        var openingBalance = snapshots.First().Balance;
        var currentBalance = snapshots.Last().Balance;

        return new AccountBalanceHistoryDto
        {
            AccountId = account.Id,
            AccountName = account.Name,
            AccountType = account.Type.ToString(),
            AccountSubType = account.SubType.ToString(),
            OpeningBalance = openingBalance,
            CurrentBalance = currentBalance,
            Snapshots = snapshots
        };
    }

    /// <inheritdoc />
    public async Task<MultiAccountBalanceHistoryDto> GetMultiAccountBalanceHistoryAsync(
        string userId,
        List<int>? accountIds,
        DateTime startDate,
        DateTime endDate,
        int intervalDays = 30,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        // Get accounts to track
        var query = _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        if (accountIds != null && accountIds.Any())
        {
            query = query.Where(a => accountIds.Contains(a.Id));
        }

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        var accounts = await query
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        // Get balance history for each account
        var accountHistories = new List<AccountBalanceHistoryDto>();

        foreach (var account in accounts)
        {
            var history = await GetAccountBalanceHistoryAsync(
                userId,
                account.Id,
                startDate,
                endDate,
                intervalDays,
                cancellationToken);

            accountHistories.Add(history);
        }

        return new MultiAccountBalanceHistoryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Accounts = accountHistories
        };
    }
}

using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.Common;
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

        // Generate balance snapshots at regular intervals using shared helper
        var snapshotDates = BalanceComputationHelper.BuildSnapshotDates(startDate, endDate, intervalDays);

        var snapshots = new List<BalanceSnapshotDto>(snapshotDates.Count);
        foreach (var date in snapshotDates)
        {
            var balance = await _ledgerService.GetAccountBalanceAsync(
                accountId, userId, date, cancellationToken);

            snapshots.Add(new BalanceSnapshotDto { Date = date, Balance = balance });
        }

        // Annotate each snapshot with change from previous
        BalanceComputationHelper.AnnotateChanges(
            snapshots,
            s => s.Balance,
            (s, change, pct) =>
            {
                s.ChangeFromPrevious = change;
                s.PercentageChangeFromPrevious = pct;
            });

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
            .ForUser(userId);

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


namespace MoneyBrain.Web.Application.Reporting.AccountBalanceHistory;

/// <summary>
/// Represents balance history for a single account over time.
/// </summary>
public class AccountBalanceHistoryDto
{
    /// <summary>
    /// Account ID.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Account type (Asset or Liability).
    /// </summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>
    /// Account sub-type.
    /// </summary>
    public string AccountSubType { get; set; } = string.Empty;

    /// <summary>
    /// Current balance (as of end date).
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// Opening balance (as of start date).
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Change in balance over the period.
    /// </summary>
    public decimal BalanceChange => CurrentBalance - OpeningBalance;

    /// <summary>
    /// Percentage change in balance.
    /// </summary>
    public decimal PercentageChange => OpeningBalance != 0 
        ? (BalanceChange / Math.Abs(OpeningBalance) * 100) 
        : 0;

    /// <summary>
    /// Historical balance snapshots.
    /// </summary>
    public List<BalanceSnapshotDto> Snapshots { get; set; } = [];

    /// <summary>
    /// Peak balance during the period.
    /// </summary>
    public decimal PeakBalance => Snapshots.Any() ? Snapshots.Max(s => s.Balance) : 0;

    /// <summary>
    /// Lowest balance during the period.
    /// </summary>
    public decimal LowestBalance => Snapshots.Any() ? Snapshots.Min(s => s.Balance) : 0;

    /// <summary>
    /// Average balance over the period.
    /// </summary>
    public decimal AverageBalance => Snapshots.Any() ? Snapshots.Average(s => s.Balance) : 0;
}

/// <summary>
/// Represents a balance snapshot at a specific point in time.
/// </summary>
public class BalanceSnapshotDto
{
    /// <summary>
    /// Date of the snapshot.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Account balance at this date.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Change from previous snapshot.
    /// </summary>
    public decimal? ChangeFromPrevious { get; set; }

    /// <summary>
    /// Percentage change from previous snapshot.
    /// </summary>
    public decimal? PercentageChangeFromPrevious { get; set; }

    /// <summary>
    /// Date display string.
    /// </summary>
    public string DateDisplay => Date.ToString("MMM dd, yyyy");
}

/// <summary>
/// Summary of balance history across multiple accounts.
/// </summary>
public class MultiAccountBalanceHistoryDto
{
    /// <summary>
    /// Start date of the reporting period.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the reporting period.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Balance histories for individual accounts.
    /// </summary>
    public List<AccountBalanceHistoryDto> Accounts { get; set; } = [];

    /// <summary>
    /// Number of accounts tracked.
    /// </summary>
    public int AccountCount => Accounts.Count;

    /// <summary>
    /// Total current balance across all accounts.
    /// </summary>
    public decimal TotalCurrentBalance => Accounts.Sum(a => a.CurrentBalance);

    /// <summary>
    /// Total opening balance across all accounts.
    /// </summary>
    public decimal TotalOpeningBalance => Accounts.Sum(a => a.OpeningBalance);

    /// <summary>
    /// Total change across all accounts.
    /// </summary>
    public decimal TotalChange => TotalCurrentBalance - TotalOpeningBalance;
}

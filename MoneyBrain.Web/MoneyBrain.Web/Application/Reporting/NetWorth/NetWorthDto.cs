namespace MoneyBrain.Web.Application.Reporting.NetWorth;

/// <summary>
/// Represents net worth snapshot at a specific point in time.
/// </summary>
public class NetWorthSnapshotDto
{
    /// <summary>
    /// Date of the snapshot.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Total value of all asset accounts.
    /// </summary>
    public decimal TotalAssets { get; set; }

    /// <summary>
    /// Total value of all liability accounts (positive number).
    /// </summary>
    public decimal TotalLiabilities { get; set; }

    /// <summary>
    /// Net worth (assets - liabilities).
    /// </summary>
    public decimal NetWorth => TotalAssets - TotalLiabilities;

    /// <summary>
    /// Breakdown by account.
    /// </summary>
    public List<AccountBalanceDto> AccountBalances { get; set; } = [];

    /// <summary>
    /// Change from previous snapshot.
    /// </summary>
    public decimal? ChangeFromPrevious { get; set; }

    /// <summary>
    /// Percentage change from previous snapshot.
    /// </summary>
    public decimal? PercentageChange { get; set; }

    /// <summary>
    /// Date display string.
    /// </summary>
    public string DateDisplay => Date.ToString("MMM dd, yyyy");
}

/// <summary>
/// Represents account balance at a specific point in time.
/// </summary>
public class AccountBalanceDto
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
    /// Account balance at this point in time.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Whether this account is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Summary of net worth history over a time period.
/// </summary>
public class NetWorthHistoryDto
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
    /// Current net worth (as of end date).
    /// </summary>
    public decimal CurrentNetWorth { get; set; }

    /// <summary>
    /// Current total assets.
    /// </summary>
    public decimal CurrentTotalAssets { get; set; }

    /// <summary>
    /// Current total liabilities.
    /// </summary>
    public decimal CurrentTotalLiabilities { get; set; }

    /// <summary>
    /// Net worth at start of period.
    /// </summary>
    public decimal StartingNetWorth { get; set; }

    /// <summary>
    /// Total change in net worth over the period.
    /// </summary>
    public decimal TotalChange => CurrentNetWorth - StartingNetWorth;

    /// <summary>
    /// Percentage change in net worth over the period.
    /// </summary>
    public decimal PercentageChange => StartingNetWorth != 0 
        ? (TotalChange / Math.Abs(StartingNetWorth) * 100) 
        : 0;

    /// <summary>
    /// Historical snapshots.
    /// </summary>
    public List<NetWorthSnapshotDto> Snapshots { get; set; } = [];

    /// <summary>
    /// Number of snapshots.
    /// </summary>
    public int SnapshotCount => Snapshots.Count;

    /// <summary>
    /// Peak net worth during the period.
    /// </summary>
    public decimal PeakNetWorth => Snapshots.Any() ? Snapshots.Max(s => s.NetWorth) : 0;

    /// <summary>
    /// Lowest net worth during the period.
    /// </summary>
    public decimal LowestNetWorth => Snapshots.Any() ? Snapshots.Min(s => s.NetWorth) : 0;

    /// <summary>
    /// Date of peak net worth.
    /// </summary>
    public DateTime? PeakDate => Snapshots.Any() 
        ? Snapshots.OrderByDescending(s => s.NetWorth).First().Date 
        : null;

    /// <summary>
    /// Date of lowest net worth.
    /// </summary>
    public DateTime? LowestDate => Snapshots.Any() 
        ? Snapshots.OrderBy(s => s.NetWorth).First().Date 
        : null;
}

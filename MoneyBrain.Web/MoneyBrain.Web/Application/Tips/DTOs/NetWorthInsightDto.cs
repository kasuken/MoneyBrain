namespace MoneyBrain.Web.Application.Tips.DTOs;

/// <summary>
/// Represents net worth trends and insights.
/// </summary>
public record NetWorthInsightDto
{
    /// <summary>
    /// Gets the insight message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current net worth.
    /// </summary>
    public decimal CurrentNetWorth { get; init; }

    /// <summary>
    /// Gets the previous period net worth.
    /// </summary>
    public decimal PreviousNetWorth { get; init; }

    /// <summary>
    /// Gets the change in net worth.
    /// </summary>
    public decimal NetWorthChange => CurrentNetWorth - PreviousNetWorth;

    /// <summary>
    /// Gets the percentage change in net worth.
    /// </summary>
    public decimal PercentageChange => PreviousNetWorth != 0 ? ((CurrentNetWorth - PreviousNetWorth) / Math.Abs(PreviousNetWorth)) * 100 : 0;

    /// <summary>
    /// Gets the total assets.
    /// </summary>
    public decimal TotalAssets { get; init; }

    /// <summary>
    /// Gets the total liabilities.
    /// </summary>
    public decimal TotalLiabilities { get; init; }

    /// <summary>
    /// Gets the debt-to-asset ratio.
    /// </summary>
    public decimal DebtToAssetRatio => TotalAssets > 0 ? (TotalLiabilities / TotalAssets) * 100 : 0;

    /// <summary>
    /// Gets historical trend data points.
    /// </summary>
    public List<NetWorthTrendDto> TrendData { get; init; } = [];

    /// <summary>
    /// Gets the date when this insight was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Represents a single data point in the net worth trend.
/// </summary>
public record NetWorthTrendDto
{
    /// <summary>
    /// Gets the date of the snapshot.
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Gets the net worth value at this date.
    /// </summary>
    public decimal NetWorth { get; init; }

    /// <summary>
    /// Gets the total assets at this date.
    /// </summary>
    public decimal Assets { get; init; }

    /// <summary>
    /// Gets the total liabilities at this date.
    /// </summary>
    public decimal Liabilities { get; init; }
}

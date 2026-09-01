using MoneyBrain.Web.Application.Transactions.Ledger;

namespace MoneyBrain.Web.Application.Reporting;

/// <summary>
/// Shared helper for generating and annotating time-series balance snapshots.
/// Used by both <see cref="AccountBalanceHistory.AccountBalanceHistoryService"/> and
/// <see cref="NetWorth.NetWorthService"/> to avoid duplicating the interval-generation
/// and change-calculation loops.
/// </summary>
internal static class BalanceComputationHelper
{
    /// <summary>
    /// Generates a list of dates (interval snapshots + always the end date) for a time range.
    /// </summary>
    /// <param name="startDate">Inclusive start of the range.</param>
    /// <param name="endDate">Inclusive end of the range.</param>
    /// <param name="intervalDays">Interval between snapshots in days (must be &gt; 0).</param>
    /// <returns>
    /// An ordered list of snapshot dates. When <paramref name="endDate"/> precedes
    /// <paramref name="startDate"/>, the list contains only <paramref name="endDate"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="intervalDays"/> is zero or negative.
    /// </exception>
    internal static IReadOnlyList<DateTime> BuildSnapshotDates(DateTime startDate, DateTime endDate, int intervalDays)
    {
        if (intervalDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalDays), intervalDays, "intervalDays must be greater than zero.");

        var dates = new List<DateTime>();
        var current = startDate;

        while (current <= endDate)
        {
            dates.Add(current);
            current = current.AddDays(intervalDays);
        }

        // Always include end date so the last data point is always present.
        if (dates.Count == 0 || dates[^1] != endDate)
        {
            dates.Add(endDate);
        }

        return dates;
    }

    /// <summary>
    /// Annotates each snapshot in the list with the change relative to the preceding snapshot.
    /// </summary>
    /// <param name="snapshots">Ordered list of snapshots (ascending date).</param>
    /// <param name="getBalance">Selector for the primary balance value used to compute the change.</param>
    /// <param name="setChange">Action that writes the computed absolute and percentage change back to the snapshot.</param>
    /// <typeparam name="T">The snapshot DTO type.</typeparam>
    internal static void AnnotateChanges<T>(
        IReadOnlyList<T> snapshots,
        Func<T, decimal> getBalance,
        Action<T, decimal, decimal> setChange)
    {
        for (int i = 1; i < snapshots.Count; i++)
        {
            var current = snapshots[i];
            var previous = snapshots[i - 1];

            var previousBalance = getBalance(previous);
            var currentBalance = getBalance(current);
            var change = currentBalance - previousBalance;
            var pctChange = previousBalance != 0
                ? change / Math.Abs(previousBalance) * 100
                : 0;

            setChange(current, change, pctChange);
        }
    }
}

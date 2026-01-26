namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Frequency for recurring transactions.
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>
    /// Transaction recurs every week.
    /// </summary>
    Weekly = 1,

    /// <summary>
    /// Transaction recurs every month.
    /// </summary>
    Monthly = 2,

    /// <summary>
    /// Transaction recurs every 3 months.
    /// </summary>
    Quarterly = 3,

    /// <summary>
    /// Transaction recurs every 6 months.
    /// </summary>
    SixMonths = 4,

    /// <summary>
    /// Transaction recurs every year.
    /// </summary>
    Yearly = 5
}

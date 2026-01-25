namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// Type of balance snapshot for categorization and filtering.
/// </summary>
public enum SnapshotType
{
    /// <summary>
    /// Manually created snapshot by user.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Automatically created snapshot (e.g., daily scheduled).
    /// </summary>
    Automatic = 2,

    /// <summary>
    /// Snapshot created after account reconciliation.
    /// </summary>
    Reconciliation = 3,

    /// <summary>
    /// Snapshot created after opening balance adjustment.
    /// </summary>
    OpeningBalanceAdjustment = 4
}

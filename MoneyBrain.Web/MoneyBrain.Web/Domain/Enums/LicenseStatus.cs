namespace MoneyBrain.Web.Domain.Enums;

/// <summary>
/// License status for user subscription validation.
/// </summary>
public enum LicenseStatus
{
    /// <summary>
    /// No license record exists.
    /// </summary>
    None = 0,

    /// <summary>
    /// User is in trial period.
    /// </summary>
    Trial = 1,

    /// <summary>
    /// User has an active paid subscription.
    /// </summary>
    Active = 2,

    /// <summary>
    /// License was active but has now expired.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// User cancelled their subscription.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// License validation failed or is invalid.
    /// </summary>
    Invalid = 5
}

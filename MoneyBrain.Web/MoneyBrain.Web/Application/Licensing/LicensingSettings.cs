namespace MoneyBrain.Web.Application.Licensing;

/// <summary>
/// Configuration settings for licensing behavior.
/// </summary>
public class LicensingSettings
{
    /// <summary>
    /// Whether licensing is enabled. When false, all users have full access.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of days to allow access after license expires (grace period).
    /// </summary>
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Number of days for the trial period.
    /// </summary>
    public int TrialDays { get; set; } = 14;
}

using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.Licensing;

/// <summary>
/// DTO containing license information for a user.
/// </summary>
public record LicenseInfo
{
    /// <summary>
    /// The user ID this license belongs to.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Current license status.
    /// </summary>
    public LicenseStatus Status { get; init; }

    /// <summary>
    /// Stripe customer ID (if available).
    /// </summary>
    public string? StripeCustomerId { get; init; }

    /// <summary>
    /// Stripe subscription ID (if available).
    /// </summary>
    public string? StripeSubscriptionId { get; init; }

    /// <summary>
    /// Name of the current plan.
    /// </summary>
    public string? PlanName { get; init; }

    /// <summary>
    /// When the license/subscription expires.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// When the trial ends (if in trial).
    /// </summary>
    public DateTime? TrialEndsAt { get; init; }

    /// <summary>
    /// Whether the user is currently in a trial period.
    /// </summary>
    public bool IsTrialing => Status == LicenseStatus.Trial;

    /// <summary>
    /// Whether the license is currently valid (active or trial).
    /// </summary>
    public bool IsActive => Status == LicenseStatus.Active || Status == LicenseStatus.Trial;

    /// <summary>
    /// Number of days remaining until expiration.
    /// </summary>
    public int? DaysRemaining => ExpiresAt.HasValue 
        ? Math.Max(0, (int)(ExpiresAt.Value - DateTime.UtcNow).TotalDays) 
        : null;
}

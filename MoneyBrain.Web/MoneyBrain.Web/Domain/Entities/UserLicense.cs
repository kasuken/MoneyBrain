using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Tracks a user's license/subscription status with Stripe integration.
/// </summary>
public class UserLicense : IUserOwnedEntity
{
    /// <summary>
    /// Unique identifier for the license record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user this license belongs to.
    /// </summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Stripe customer ID for this user.
    /// </summary>
    [MaxLength(255)]
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// Stripe subscription ID for active subscriptions.
    /// </summary>
    [MaxLength(255)]
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Current license status.
    /// </summary>
    public LicenseStatus Status { get; set; } = LicenseStatus.None;

    /// <summary>
    /// Name of the plan (e.g., "Monthly", "Yearly", "Lifetime").
    /// </summary>
    [MaxLength(100)]
    public string? PlanName { get; set; }

    /// <summary>
    /// When the trial started (if applicable).
    /// </summary>
    public DateTime? TrialStartedAt { get; set; }

    /// <summary>
    /// When the trial ends (if applicable).
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>
    /// When the subscription started.
    /// </summary>
    public DateTime? SubscriptionStartedAt { get; set; }

    /// <summary>
    /// When the subscription ends/renews.
    /// </summary>
    public DateTime? SubscriptionEndsAt { get; set; }

    /// <summary>
    /// When the license record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the license record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the license was last validated against Stripe.
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }
}

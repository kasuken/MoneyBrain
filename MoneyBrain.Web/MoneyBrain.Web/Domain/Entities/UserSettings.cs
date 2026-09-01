using System.ComponentModel.DataAnnotations;
using MoneyBrain.Web.Application.Common.Interfaces;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// User-specific settings including currency preference and timezone.
/// Every authenticated user must have settings configured before using the app.
/// </summary>
public class UserSettings : IUserOwnedEntity
{
    /// <summary>
    /// Unique identifier for the settings record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user these settings belong to. One user = one settings record.
    /// </summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// User's preferred currency code (ISO 4217, e.g., "USD", "EUR", "GBP").
    /// Used for displaying monetary values throughout the app.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public required string CurrencyCode { get; set; }

    /// <summary>
    /// User's preferred timezone (IANA timezone ID, e.g., "America/New_York", "Europe/London").
    /// Used for displaying dates and times in local time.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string TimeZoneId { get; set; }

    /// <summary>
    /// Date format preference (e.g., "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd").
    /// </summary>
    [MaxLength(20)]
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    /// Whether setup wizard has been completed.
    /// </summary>
    public bool SetupCompleted { get; set; } = false;

    /// <summary>
    /// Whether to show tips and insights features.
    /// </summary>
    public bool ShowTipsAndInsights { get; set; } = true;

    /// <summary>
    /// Whether to show educational tips.
    /// </summary>
    public bool ShowEducationalTips { get; set; } = true;

    /// <summary>
    /// Whether to show spending insights.
    /// </summary>
    public bool ShowSpendingInsights { get; set; } = true;

    /// <summary>
    /// Whether to show behavioral insights.
    /// </summary>
    public bool ShowBehavioralInsights { get; set; } = true;

    /// <summary>
    /// When the settings were created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the settings were last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

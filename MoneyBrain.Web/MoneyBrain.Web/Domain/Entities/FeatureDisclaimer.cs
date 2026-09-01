using System.ComponentModel.DataAnnotations;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Store disclaimers for features (legal/compliance requirement).
/// </summary>
public class FeatureDisclaimer
{
    /// <summary>
    /// Unique identifier for the disclaimer.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Feature name (e.g., "TipsInsights", "SpendingAnalysis").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public required string Feature { get; set; }

    /// <summary>
    /// Disclaimer text content.
    /// </summary>
    [Required]
    public required string DisclaimerText { get; set; }

    /// <summary>
    /// Localization key for the disclaimer - REQUIRED for all disclaimers.
    /// </summary>
    [Required]
    [MaxLength(150)]
    public required string LocalizationKey { get; set; }

    /// <summary>
    /// Whether the disclaimer is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the disclaimer was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

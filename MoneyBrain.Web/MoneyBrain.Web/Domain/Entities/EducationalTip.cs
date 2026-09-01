using System.ComponentModel.DataAnnotations;

namespace MoneyBrain.Web.Domain.Entities;

/// <summary>
/// Curated content library for educational tips.
/// </summary>
public class EducationalTip
{
    /// <summary>
    /// Unique identifier for the educational tip.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Title of the educational tip.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    /// <summary>
    /// Content of the educational tip.
    /// </summary>
    [Required]
    public required string Content { get; set; }

    /// <summary>
    /// Category of the tip (e.g., "Budgeting", "Saving", "Investing").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public required string Category { get; set; }

    /// <summary>
    /// Display order for presentation sequence in UI.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether the tip is currently active and should be shown.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional localization key - when present, title/content should be pulled from SharedResource.resx.
    /// </summary>
    [MaxLength(150)]
    public string? LocalizationKey { get; set; }

    /// <summary>
    /// When the tip was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the tip was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

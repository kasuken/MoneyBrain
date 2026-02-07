namespace MoneyBrain.Web.Application.Tips.DTOs;

/// <summary>
/// Represents an educational financial tip for users.
/// </summary>
public record EducationalTipDto
{
    /// <summary>
    /// Gets the unique identifier of the tip.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the title of the tip.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the descriptive content of the tip.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the category of the tip (e.g., "Budgeting", "Saving", "Investing").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the priority level (1-5, where 5 is highest priority).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tip is currently active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the date when the tip was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets optional tags associated with the tip.
    /// </summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets optional URL for additional resources.
    /// </summary>
    public string? ResourceUrl { get; init; }
}

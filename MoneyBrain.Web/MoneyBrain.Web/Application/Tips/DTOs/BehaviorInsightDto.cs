namespace MoneyBrain.Web.Application.Tips.DTOs;

/// <summary>
/// Represents behavioral patterns and insights based on financial habits.
/// </summary>
public record BehaviorInsightDto
{
    /// <summary>
    /// Gets the insight message describing the behavior pattern.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the behavior category (e.g., "Spending Pattern", "Saving Habit").
    /// </summary>
    public string BehaviorType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the severity or importance level (1-5, where 5 is most important).
    /// </summary>
    public int Severity { get; init; }

    /// <summary>
    /// Gets the period analyzed (e.g., "Last 90 days").
    /// </summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>
    /// Gets the observed pattern description.
    /// </summary>
    public string PatternDescription { get; init; } = string.Empty;

    /// <summary>
    /// Gets suggested actions to improve the behavior.
    /// </summary>
    public List<string> SuggestedActions { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether this represents a positive behavior.
    /// </summary>
    public bool IsPositive { get; init; }

    /// <summary>
    /// Gets optional metrics supporting the insight.
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; init; } = new();

    /// <summary>
    /// Gets the date when this insight was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }
}

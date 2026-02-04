namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// Aggregation configuration for summarizing query results
/// </summary>
public class AggregationConfig
{
    /// <summary>
    /// Aggregation function to apply
    /// </summary>
    public AggregationFunction Function { get; set; }

    /// <summary>
    /// Property to aggregate (e.g., "Amount")
    /// </summary>
    public required string Property { get; set; }

    /// <summary>
    /// Group by properties (e.g., ["Category.Name", "Date.Month"])
    /// </summary>
    public List<string> GroupBy { get; set; } = [];
}

/// <summary>
/// Supported aggregation functions
/// </summary>
public enum AggregationFunction
{
    Sum,
    Count,
    Average,
    Min,
    Max
}

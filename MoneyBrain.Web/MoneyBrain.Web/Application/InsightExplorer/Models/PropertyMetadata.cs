namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// Metadata about a queryable property
/// </summary>
public class PropertyMetadata
{
    /// <summary>
    /// Property path (e.g., "Amount", "Category.Name")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name for UI
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Data type of the property
    /// </summary>
    public PropertyDataType DataType { get; set; }

    /// <summary>
    /// Whether this property can be used in filters
    /// </summary>
    public bool IsFilterable { get; set; } = true;

    /// <summary>
    /// Whether this property can be aggregated (Sum, Avg, etc.)
    /// </summary>
    public bool IsAggregatable { get; set; }

    /// <summary>
    /// Whether this property can be used for grouping
    /// </summary>
    public bool IsGroupable { get; set; }

    /// <summary>
    /// Possible values for enum properties
    /// </summary>
    public List<string>? EnumValues { get; set; }
}

/// <summary>
/// Result of query validation
/// </summary>
public class QueryValidationResult
{
    /// <summary>
    /// Whether the query is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation error messages
    /// </summary>
    public List<string> Errors { get; set; } = [];
}

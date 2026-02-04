namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// A single filter condition in a query
/// </summary>
public class FilterCondition
{
    /// <summary>
    /// Property path to filter on (e.g., "Amount", "Category.Name", "Date")
    /// </summary>
    public required string Property { get; set; }

    /// <summary>
    /// Comparison operator
    /// </summary>
    public FilterOperator Operator { get; set; }

    /// <summary>
    /// Value to compare against (JSON-serialized for complex types)
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Secondary value for Between operator
    /// </summary>
    public string? SecondValue { get; set; }

    /// <summary>
    /// Data type of the property for proper comparison
    /// </summary>
    public PropertyDataType DataType { get; set; } = PropertyDataType.String;
}

/// <summary>
/// Supported filter operators
/// </summary>
public enum FilterOperator
{
    // Equality
    Equals,
    NotEquals,

    // String operators
    Contains,
    StartsWith,
    EndsWith,

    // Comparison operators
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,

    // Range operators
    Between,

    // Null checks
    IsNull,
    IsNotNull,

    // Collection operators
    InList
}

/// <summary>
/// Data types for property values
/// </summary>
public enum PropertyDataType
{
    String,
    Integer,
    Decimal,
    DateTime,
    Boolean,
    Enum
}

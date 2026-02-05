namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// Defines a dynamic query configuration for the Insight Explorer
/// </summary>
public class QueryDefinition
{
    /// <summary>
    /// Target entity type for the query (Transaction, Account, Category, etc.)
    /// </summary>
    public QueryTargetEntity TargetEntity { get; set; } = QueryTargetEntity.Transaction;

    /// <summary>
    /// List of filter conditions (AND logic by default)
    /// </summary>
    public List<FilterCondition> Filters { get; set; } = [];

    /// <summary>
    /// Aggregation configuration (optional)
    /// </summary>
    public AggregationConfig? Aggregation { get; set; }

    /// <summary>
    /// Chart configuration (optional, used when viewing as chart)
    /// </summary>
    public ChartConfig? Chart { get; set; }

    /// <summary>
    /// Columns to display in data table view
    /// </summary>
    public List<string> DisplayColumns { get; set; } = [];

    /// <summary>
    /// Sort configuration
    /// </summary>
    public SortConfig? Sort { get; set; }
}

/// <summary>
/// Target entity types for queries
/// </summary>
public enum QueryTargetEntity
{
    Transaction,
    Account,
    Category,
    Payee,
    Budget
}

/// <summary>
/// Sort configuration for query results
/// </summary>
public class SortConfig
{
    /// <summary>
    /// Property to sort by
    /// </summary>
    public required string Property { get; set; }

    /// <summary>
    /// Sort direction (true = ascending, false = descending)
    /// </summary>
    public bool Ascending { get; set; } = true;
}

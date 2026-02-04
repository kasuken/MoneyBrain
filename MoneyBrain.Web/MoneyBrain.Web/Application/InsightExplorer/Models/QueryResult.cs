namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// Result of executing a query
/// </summary>
public class QueryResult
{
    /// <summary>
    /// Raw data rows (for table display)
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>
    /// Aggregated data (when aggregation is configured)
    /// </summary>
    public List<AggregatedRow> AggregatedRows { get; set; } = [];

    /// <summary>
    /// Chart-ready data
    /// </summary>
    public ChartData? ChartData { get; set; }

    /// <summary>
    /// Total count before pagination
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Column metadata for display
    /// </summary>
    public List<ColumnMetadata> Columns { get; set; } = [];
}

/// <summary>
/// A row of aggregated data
/// </summary>
public class AggregatedRow
{
    /// <summary>
    /// Group key values
    /// </summary>
    public Dictionary<string, object?> GroupKeys { get; set; } = new();

    /// <summary>
    /// Aggregated value for this group
    /// </summary>
    public decimal AggregatedValue { get; set; }
}

/// <summary>
/// Chart-ready data structure
/// </summary>
public class ChartData
{
    /// <summary>
    /// Labels for the chart (X-axis or pie segments)
    /// </summary>
    public string[] Labels { get; set; } = [];

    /// <summary>
    /// Data values (single series)
    /// </summary>
    public double[] Data { get; set; } = [];

    /// <summary>
    /// Multiple series data (for multi-series charts)
    /// </summary>
    public List<ChartSeriesData>? Series { get; set; }
}

/// <summary>
/// Data for a single chart series
/// </summary>
public class ChartSeriesData
{
    /// <summary>
    /// Series name/label
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Series data values
    /// </summary>
    public double[] Data { get; set; } = [];
}

/// <summary>
/// Metadata for a result column
/// </summary>
public class ColumnMetadata
{
    /// <summary>
    /// Column property name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name for the column header
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Data type of the column
    /// </summary>
    public PropertyDataType DataType { get; set; }

    /// <summary>
    /// Whether this column is sortable
    /// </summary>
    public bool Sortable { get; set; } = true;
}

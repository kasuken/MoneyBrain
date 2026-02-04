namespace MoneyBrain.Web.Application.InsightExplorer.Models;

/// <summary>
/// Chart visualization configuration
/// </summary>
public class ChartConfig
{
    /// <summary>
    /// Type of chart to render
    /// </summary>
    public ChartType Type { get; set; } = ChartType.Bar;

    /// <summary>
    /// Property for X-axis (or labels for pie/donut)
    /// </summary>
    public required string XAxisProperty { get; set; }

    /// <summary>
    /// Property for Y-axis (or values for pie/donut)
    /// </summary>
    public required string YAxisProperty { get; set; }

    /// <summary>
    /// Optional series grouping property
    /// </summary>
    public string? SeriesProperty { get; set; }

    /// <summary>
    /// Chart title
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
/// Supported chart types (aligned with MudBlazor ChartType)
/// </summary>
public enum ChartType
{
    Bar,
    Line,
    Pie,
    Donut
}

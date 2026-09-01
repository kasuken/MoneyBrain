#nullable enable
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Resources;
using MudBlazor;
using InsightChartType = MoneyBrain.Web.Application.InsightExplorer.Models.ChartType;
using InsightFilterOperator = MoneyBrain.Web.Application.InsightExplorer.Models.FilterOperator;

namespace MoneyBrain.Web.Components.Pages;

public partial class InsightExplorer
{
    [Inject] private IInsightExplorerService InsightExplorerService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;

    private string? _userId;
    private QueryDefinition _query = new();
    private QueryResult? _result;
    private List<PropertyMetadata> _availableProperties = [];
    private List<SavedQuery> _savedQueries = [];
    private Dictionary<int, DateTime?> _filterDates = new();
    private Dictionary<int, DateTime?> _filterSecondDates = new();

    private bool _isLoading;
    private bool _hasChanges;
    private bool _hasExecuted;
    private string? _loadedQueryName;
    private int? _loadedQueryId;

    // Aggregation state
    private bool _useAggregation;
    private AggregationFunction _aggregationFunction = AggregationFunction.Sum;
    private string _aggregationProperty = "Amount";
    private string _groupByProperty = "";

    // Chart state
    private bool _useChart;
    private InsightChartType _chartType = InsightChartType.Bar;
    private string _chartTitle = "";
    private ChartOptions _chartOptions = new() { YAxisTicks = 5 };
    private List<ChartSeries> _chartSeries = [];

    // Save dialog state
    private bool _showSaveDialog;
    private string _saveQueryName = "";
    private string _saveQueryDescription = "";

    // Load dialog state
    private bool _showLoadDialog;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        UpdateAvailableProperties();
    }

    private void UpdateAvailableProperties()
    {
        _availableProperties = InsightExplorerService.GetEntityProperties(_query.TargetEntity);
    }

    private void OnTargetEntityChanged(QueryTargetEntity entity)
    {
        _query.TargetEntity = entity;
        _query.Filters.Clear();
        _filterDates.Clear();
        _filterSecondDates.Clear();
        UpdateAvailableProperties();
        MarkAsChanged();
    }

    private void AddFilter()
    {
        var defaultProp = _availableProperties.FirstOrDefault(p => p.IsFilterable);
        _query.Filters.Add(new FilterCondition
        {
            Property = defaultProp?.Name ?? "",
            DataType = defaultProp?.DataType ?? PropertyDataType.String,
            Operator = InsightFilterOperator.Equals
        });
        MarkAsChanged();
    }

    private void OnPropertyChanged(int index, string? value)
    {
        if (index >= 0 && index < _query.Filters.Count)
        {
            _query.Filters[index].Property = value ?? "";
            var prop = _availableProperties.FirstOrDefault(p => p.Name == value);
            if (prop != null)
            {
                _query.Filters[index].DataType = prop.DataType;
            }
        }
        MarkAsChanged();
    }

    private void OnOperatorChanged(int index, InsightFilterOperator op)
    {
        if (index >= 0 && index < _query.Filters.Count)
        {
            _query.Filters[index].Operator = op;
        }
        MarkAsChanged();
    }

    private void OnValueChanged(int index, string? value)
    {
        if (index >= 0 && index < _query.Filters.Count)
        {
            _query.Filters[index].Value = value;
        }
        MarkAsChanged();
    }

    private void OnSecondValueChanged(int index, string? value)
    {
        if (index >= 0 && index < _query.Filters.Count)
        {
            _query.Filters[index].SecondValue = value;
        }
        MarkAsChanged();
    }

    private void RemoveFilter(int index)
    {
        if (index >= 0 && index < _query.Filters.Count)
        {
            _query.Filters.RemoveAt(index);
            _filterDates.Remove(index);
            _filterSecondDates.Remove(index);
            MarkAsChanged();
        }
    }

    private void OnFilterChanged()
    {
        // Update data type when property changes
        foreach (var filter in _query.Filters)
        {
            var prop = _availableProperties.FirstOrDefault(p => p.Name == filter.Property);
            if (prop != null)
            {
                filter.DataType = prop.DataType;
            }
        }
        MarkAsChanged();
    }

    private void OnDateFilterChanged(int index, DateTime? date)
    {
        _filterDates[index] = date;
        if (index < _query.Filters.Count)
        {
            _query.Filters[index].Value = date?.ToString("O");
        }
        MarkAsChanged();
    }

    private void OnSecondDateFilterChanged(int index, DateTime? date)
    {
        _filterSecondDates[index] = date;
        if (index < _query.Filters.Count)
        {
            _query.Filters[index].SecondValue = date?.ToString("O");
        }
        MarkAsChanged();
    }

    private void OnAggregationToggled(bool enabled)
    {
        _useAggregation = enabled;
        UpdateAggregationConfig();
        MarkAsChanged();
    }

    private void OnAggregationFunctionChanged(AggregationFunction func)
    {
        _aggregationFunction = func;
        UpdateAggregationConfig();
        MarkAsChanged();
    }

    private void OnAggregationPropertyChanged(string prop)
    {
        _aggregationProperty = prop;
        UpdateAggregationConfig();
        MarkAsChanged();
    }

    private void OnGroupByChanged(string prop)
    {
        _groupByProperty = prop;
        UpdateAggregationConfig();
        MarkAsChanged();
    }

    private void UpdateAggregationConfig()
    {
        if (_useAggregation)
        {
            _query.Aggregation = new AggregationConfig
            {
                Function = _aggregationFunction,
                Property = _aggregationProperty,
                GroupBy = string.IsNullOrEmpty(_groupByProperty) ? [] : [_groupByProperty]
            };
        }
        else
        {
            _query.Aggregation = null;
        }
    }

    private void OnChartToggled(bool enabled)
    {
        _useChart = enabled;
        UpdateChartConfig();
        MarkAsChanged();
    }

    private void OnChartTypeChanged(InsightChartType type)
    {
        _chartType = type;
        UpdateChartConfig();
        MarkAsChanged();
    }

    private void OnChartTitleChanged(string? title)
    {
        _chartTitle = title ?? "";
        UpdateChartConfig();
        MarkAsChanged();
    }

    private void UpdateChartConfig()
    {
        if (_useChart && _useAggregation && !string.IsNullOrEmpty(_groupByProperty))
        {
            _query.Chart = new ChartConfig
            {
                Type = _chartType,
                XAxisProperty = _groupByProperty,
                YAxisProperty = _aggregationProperty,
                Title = _chartTitle
            };
        }
        else
        {
            _query.Chart = null;
        }
    }

    private void BuildChartSeries()
    {
        _chartSeries.Clear();

        if (_result?.ChartData == null || _result.ChartData.Data.Length == 0)
            return;

        // Build ChartSeries for Line/Bar charts
        _chartSeries.Add(new ChartSeries
        {
            Name = _aggregationProperty,
            Data = _result.ChartData.Data
        });
    }

    private void MarkAsChanged()
    {
        _hasChanges = true;
    }

    private async Task ExecuteQuery()
    {
        if (string.IsNullOrEmpty(_userId))
            return;

        var validation = InsightExplorerService.ValidateQuery(_query);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                Snackbar.Add(error, Severity.Warning);
            }
            return;
        }

        _isLoading = true;
        _hasExecuted = true;
        StateHasChanged();

        try
        {
            _result = await InsightExplorerService.ExecuteQueryAsync(_userId, _query);
            BuildChartSeries();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error executing query: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void OpenSaveDialog()
    {
        _saveQueryName = _loadedQueryName ?? "";
        _saveQueryDescription = "";
        _showSaveDialog = true;
    }

    private async Task SaveQuery()
    {
        if (string.IsNullOrEmpty(_userId) || string.IsNullOrWhiteSpace(_saveQueryName))
            return;

        try
        {
            if (_loadedQueryId.HasValue)
            {
                await InsightExplorerService.UpdateQueryAsync(_loadedQueryId.Value, _userId, _saveQueryName, _saveQueryDescription, _query);
                Snackbar.Add("Query updated successfully", Severity.Success);
            }
            else
            {
                var saved = await InsightExplorerService.SaveQueryAsync(_userId, _saveQueryName, _saveQueryDescription, _query);
                _loadedQueryId = saved.Id;
                Snackbar.Add("Query saved successfully", Severity.Success);
            }

            _loadedQueryName = _saveQueryName;
            _hasChanges = false;
            _showSaveDialog = false;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving query: {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenLoadDialog()
    {
        if (string.IsNullOrEmpty(_userId))
            return;

        try
        {
            _savedQueries = await InsightExplorerService.GetSavedQueriesAsync(_userId);
            _showLoadDialog = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading queries: {ex.Message}", Severity.Error);
        }
    }

    private void LoadQuery(SavedQuery savedQuery)
    {
        var deserializedQuery = InsightExplorerService.DeserializeQuery(savedQuery.QueryDefinitionJson);
        if (deserializedQuery != null)
        {
            _query = deserializedQuery;
            _loadedQueryId = savedQuery.Id;
            _loadedQueryName = savedQuery.Name;

            // Update UI state from loaded query
            UpdateAvailableProperties();
            _useAggregation = _query.Aggregation != null;
            if (_query.Aggregation != null)
            {
                _aggregationFunction = _query.Aggregation.Function;
                _aggregationProperty = _query.Aggregation.Property;
                _groupByProperty = _query.Aggregation.GroupBy.FirstOrDefault() ?? "";
            }

            _useChart = _query.Chart != null;
            if (_query.Chart != null)
            {
                _chartType = _query.Chart.Type;
                _chartTitle = _query.Chart.Title ?? "";
            }

            // Update filter dates
            _filterDates.Clear();
            _filterSecondDates.Clear();
            for (int i = 0; i < _query.Filters.Count; i++)
            {
                var filter = _query.Filters[i];
                if (filter.DataType == PropertyDataType.DateTime)
                {
                    if (DateTime.TryParse(filter.Value, out var d1))
                        _filterDates[i] = d1;
                    if (DateTime.TryParse(filter.SecondValue, out var d2))
                        _filterSecondDates[i] = d2;
                }
            }

            _hasChanges = false;
            _result = null;
            _hasExecuted = false;
            _showLoadDialog = false;

            Snackbar.Add($"Loaded query: {savedQuery.Name}", Severity.Success);
        }
    }

    private async Task DeleteSavedQuery(SavedQuery savedQuery)
    {
        if (string.IsNullOrEmpty(_userId))
            return;

        var confirmed = await DialogService.ShowMessageBox(
            L["Insight_DeleteQueryTitle"],
            string.Format(L["Insight_DeleteQueryConfirm"], savedQuery.Name),
            yesText: L["Btn_Delete"],
            cancelText: L["Btn_Cancel"]);

        if (confirmed == true)
        {
            try
            {
                await InsightExplorerService.DeleteQueryAsync(savedQuery.Id, _userId);
                _savedQueries.Remove(savedQuery);

                if (_loadedQueryId == savedQuery.Id)
                {
                    _loadedQueryId = null;
                    _loadedQueryName = null;
                }

                Snackbar.Add("Query deleted", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error deleting query: {ex.Message}", Severity.Error);
            }
        }
    }

    private MudBlazor.ChartType GetMudChartType()
    {
        return _chartType switch
        {
            InsightChartType.Bar => MudBlazor.ChartType.Bar,
            InsightChartType.Line => MudBlazor.ChartType.Line,
            InsightChartType.Pie => MudBlazor.ChartType.Pie,
            InsightChartType.Donut => MudBlazor.ChartType.Donut,
            _ => MudBlazor.ChartType.Bar
        };
    }

    private static IEnumerable<InsightFilterOperator> GetOperatorsForDataType(PropertyDataType dataType)
    {
        return dataType switch
        {
            PropertyDataType.String =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals,
                InsightFilterOperator.Contains, InsightFilterOperator.StartsWith, InsightFilterOperator.EndsWith,
                InsightFilterOperator.IsNull, InsightFilterOperator.IsNotNull
            ],
            PropertyDataType.Integer or PropertyDataType.Decimal =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals,
                InsightFilterOperator.GreaterThan, InsightFilterOperator.LessThan,
                InsightFilterOperator.GreaterOrEqual, InsightFilterOperator.LessOrEqual,
                InsightFilterOperator.Between
            ],
            PropertyDataType.DateTime =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals,
                InsightFilterOperator.GreaterThan, InsightFilterOperator.LessThan,
                InsightFilterOperator.GreaterOrEqual, InsightFilterOperator.LessOrEqual,
                InsightFilterOperator.Between
            ],
            PropertyDataType.Boolean =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals
            ],
            PropertyDataType.Enum =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals
            ],
            _ =>
            [
                InsightFilterOperator.Equals, InsightFilterOperator.NotEquals
            ]
        };
    }

    private static string FormatOperator(InsightFilterOperator op)
    {
        return op switch
        {
            InsightFilterOperator.Equals => "=",
            InsightFilterOperator.NotEquals => "≠",
            InsightFilterOperator.Contains => "∋",
            InsightFilterOperator.StartsWith => "^",
            InsightFilterOperator.EndsWith => "$",
            InsightFilterOperator.GreaterThan => ">",
            InsightFilterOperator.LessThan => "<",
            InsightFilterOperator.GreaterOrEqual => ">=",
            InsightFilterOperator.LessOrEqual => "<=",
            InsightFilterOperator.Between => "↔",
            InsightFilterOperator.IsNull => "∅",
            InsightFilterOperator.IsNotNull => "!∅",
            InsightFilterOperator.InList => "∈",
            _ => op.ToString()
        };
    }

    private static object? GetAggregatedValue(AggregatedRow row, string columnName)
    {
        if (columnName == "Value")
            return row.AggregatedValue;

        return row.GroupKeys.GetValueOrDefault(columnName);
    }

    private static string FormatValue(object? value, PropertyDataType dataType)
    {
        if (value == null)
            return "";

        return dataType switch
        {
            PropertyDataType.DateTime when value is DateTimeOffset dto => dto.ToString("g"),
            PropertyDataType.DateTime when value is DateTime dt => dt.ToString("g"),
            PropertyDataType.Decimal when value is decimal d => d.ToString("N2"),
            PropertyDataType.Boolean when value is bool b => b ? "✓" : "✗",
            _ => value.ToString() ?? ""
        };
    }
}

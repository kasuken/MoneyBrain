using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Domain.Entities;
using MoneyBrain.Web.Domain.Enums;

namespace MoneyBrain.Web.Application.InsightExplorer;

/// <summary>
/// Implementation of Insight Explorer service for dynamic queries
/// </summary>
public class InsightExplorerService(ApplicationDbContext context) : IInsightExplorerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Task<QueryResult> ExecuteQueryAsync(
        string userId,
        QueryDefinition query,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        return query.TargetEntity switch
        {
            QueryTargetEntity.Transaction => ExecuteTransactionQueryAsync(userId, query, page, pageSize, cancellationToken),
            QueryTargetEntity.Account => ExecuteAccountQueryAsync(userId, query, page, pageSize, cancellationToken),
            QueryTargetEntity.Category => ExecuteCategoryQueryAsync(userId, query, page, pageSize, cancellationToken),
            QueryTargetEntity.Payee => ExecutePayeeQueryAsync(userId, query, page, pageSize, cancellationToken),
            QueryTargetEntity.Budget => ExecuteBudgetQueryAsync(userId, query, page, pageSize, cancellationToken),
            _ => Task.FromResult(new QueryResult())
        };
    }

    private async Task<QueryResult> ExecuteTransactionQueryAsync(
        string userId,
        QueryDefinition query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .ThenInclude(c => c!.CategoryGroup)
            .Include(t => t.Payee)
            .Where(t => t.UserId == userId)
            .AsNoTracking();

        // Apply filters
        baseQuery = ApplyFilters(baseQuery, query.Filters);

        // Get total count
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var result = new QueryResult { TotalCount = totalCount };

        // Apply sorting
        if (query.Sort != null)
        {
            baseQuery = ApplySort(baseQuery, query.Sort);
        }
        else
        {
            baseQuery = baseQuery.OrderByDescending(t => t.Date);
        }

        // Handle aggregation
        if (query.Aggregation != null)
        {
            var aggregatedResult = await ExecuteTransactionAggregationAsync(baseQuery, query.Aggregation, query.Chart, cancellationToken);
            aggregatedResult.TotalCount = totalCount;
            return aggregatedResult;
        }

        // Paginate and project to dictionary
        var transactions = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Build column metadata
        result.Columns = GetTransactionColumns(query.DisplayColumns);

        // Project to dictionary rows
        foreach (var t in transactions)
        {
            result.Rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = t.Id,
                ["Date"] = t.Date,
                ["Amount"] = t.Amount,
                ["Payee"] = t.Payee?.Name,
                ["Category"] = t.Category?.Name,
                ["CategoryGroup"] = t.Category?.CategoryGroup?.Name,
                ["Account"] = t.Account?.Name,
                ["Memo"] = t.Memo,
                ["Status"] = t.Status.ToString(),
                ["IsCleared"] = t.IsCleared,
                ["IsReconciled"] = t.IsReconciled,
                ["ReferenceNumber"] = t.ReferenceNumber,
                ["Tags"] = t.Tags
            });
        }

        return result;
    }

    private async Task<QueryResult> ExecuteTransactionAggregationAsync(
        IQueryable<Transaction> baseQuery,
        AggregationConfig aggregation,
        ChartConfig? chartConfig,
        CancellationToken cancellationToken)
    {
        var result = new QueryResult();

        // Group by first group property if specified
        if (aggregation.GroupBy.Count > 0)
        {
            var groupProperty = aggregation.GroupBy[0];
            var groupedData = await ExecuteGroupedAggregationAsync(baseQuery, groupProperty, aggregation, cancellationToken);

            foreach (var item in groupedData)
            {
                result.AggregatedRows.Add(new AggregatedRow
                {
                    GroupKeys = new Dictionary<string, object?> { [groupProperty] = item.Key },
                    AggregatedValue = item.Value
                });
            }

            // Build chart data if configured
            if (chartConfig != null)
            {
                result.ChartData = new ChartData
                {
                    Labels = groupedData.Select(g => g.Key).ToArray(),
                    Data = groupedData.Select(g => (double)g.Value).ToArray()
                };
            }

            result.Columns =
            [
                new ColumnMetadata { Name = groupProperty, DisplayName = GetDisplayName(groupProperty), DataType = PropertyDataType.String },
                new ColumnMetadata { Name = "Value", DisplayName = $"{aggregation.Function} of {aggregation.Property}", DataType = PropertyDataType.Decimal }
            ];
        }
        else
        {
            // No grouping - single aggregate value
            decimal value = aggregation.Function switch
            {
                AggregationFunction.Sum => await baseQuery.SumAsync(t => t.Amount, cancellationToken),
                AggregationFunction.Count => await baseQuery.CountAsync(cancellationToken),
                AggregationFunction.Average => await baseQuery.AverageAsync(t => t.Amount, cancellationToken),
                AggregationFunction.Min => await baseQuery.MinAsync(t => t.Amount, cancellationToken),
                AggregationFunction.Max => await baseQuery.MaxAsync(t => t.Amount, cancellationToken),
                _ => 0m
            };

            result.AggregatedRows.Add(new AggregatedRow { AggregatedValue = value });
            result.Columns =
            [
                new ColumnMetadata { Name = "Value", DisplayName = $"{aggregation.Function} of {aggregation.Property}", DataType = PropertyDataType.Decimal }
            ];
        }

        return result;
    }

    private async Task<List<(string Key, decimal Value)>> ExecuteGroupedAggregationAsync(
        IQueryable<Transaction> baseQuery,
        string groupProperty,
        AggregationConfig aggregation,
        CancellationToken cancellationToken)
    {
        // First, materialize the data we need for grouping and aggregation
        IQueryable<(string Key, decimal Amount)> projected = groupProperty switch
        {
            "Category.Name" => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Category != null && t.Category.Name != null ? t.Category.Name : "Uncategorized", t.Amount)),

            "Category.CategoryGroup.Name" => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Category != null && t.Category.CategoryGroup != null && t.Category.CategoryGroup.Name != null
                    ? t.Category.CategoryGroup.Name
                    : "Uncategorized", t.Amount)),

            "Account.Name" => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Account != null && t.Account.Name != null ? t.Account.Name : "Unknown", t.Amount)),

            "Payee.Name" => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Payee != null && t.Payee.Name != null ? t.Payee.Name : "No Payee", t.Amount)),

            "Status" => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Status.ToString(), t.Amount)),

            _ => baseQuery.Select(t => new ValueTuple<string, decimal>(
                t.Date.Year + "-" + t.Date.Month.ToString().PadLeft(2, '0'), t.Amount))
        };

        // Materialize and group in memory
        var materializedData = await projected.ToListAsync(cancellationToken);

        var grouped = materializedData
            .GroupBy(x => x.Item1)
            .Select(g => (
                Key: g.Key,
                Value: aggregation.Function switch
                {
                    AggregationFunction.Sum => g.Sum(x => x.Item2),
                    AggregationFunction.Count => g.Count(),
                    AggregationFunction.Average => g.Average(x => x.Item2),
                    AggregationFunction.Min => g.Min(x => x.Item2),
                    AggregationFunction.Max => g.Max(x => x.Item2),
                    _ => 0m
                }
            ))
            .OrderBy(x => x.Key)
            .ToList();

        return grouped;
    }

    private async Task<QueryResult> ExecuteAccountQueryAsync(
        string userId,
        QueryDefinition query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.Accounts
            .Where(a => a.UserId == userId)
            .AsNoTracking();

        baseQuery = ApplyFilters(baseQuery, query.Filters);

        var result = new QueryResult
        {
            TotalCount = await baseQuery.CountAsync(cancellationToken)
        };

        var accounts = await baseQuery
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        result.Columns = GetAccountColumns();

        foreach (var a in accounts)
        {
            result.Rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = a.Id,
                ["Name"] = a.Name,
                ["Type"] = a.Type.ToString(),
                ["SubType"] = a.SubType.ToString(),
                ["Group"] = a.Group,
                ["OpeningBalance"] = a.OpeningBalance,
                ["IsActive"] = a.IsActive,
                ["CurrencyCode"] = a.CurrencyCode,
                ["MonthlySpendingLimit"] = a.MonthlySpendingLimit,
                ["CreatedAt"] = a.CreatedAt
            });
        }

        return result;
    }

    private async Task<QueryResult> ExecuteCategoryQueryAsync(
        string userId,
        QueryDefinition query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.Categories
            .Include(c => c.CategoryGroup)
            .Where(c => c.UserId == userId)
            .AsNoTracking();

        baseQuery = ApplyFilters(baseQuery, query.Filters);

        var result = new QueryResult
        {
            TotalCount = await baseQuery.CountAsync(cancellationToken)
        };

        var categories = await baseQuery
            .OrderBy(c => c.CategoryGroup!.Name)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        result.Columns = GetCategoryColumns();

        foreach (var c in categories)
        {
            result.Rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = c.Id,
                ["Name"] = c.Name,
                ["GroupName"] = c.CategoryGroup?.Name,
                ["GroupType"] = c.CategoryGroup?.Type.ToString(),
                ["IsActive"] = c.IsActive,
                ["SortOrder"] = c.SortOrder
            });
        }

        return result;
    }

    private async Task<QueryResult> ExecutePayeeQueryAsync(
        string userId,
        QueryDefinition query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.Payees
            .Include(p => p.DefaultCategory)
            .Where(p => p.UserId == userId)
            .AsNoTracking();

        baseQuery = ApplyFilters(baseQuery, query.Filters);

        var result = new QueryResult
        {
            TotalCount = await baseQuery.CountAsync(cancellationToken)
        };

        var payees = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        result.Columns = GetPayeeColumns();

        foreach (var p in payees)
        {
            result.Rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = p.Id,
                ["Name"] = p.Name,
                ["DefaultCategory"] = p.DefaultCategory?.Name,
                ["IsActive"] = p.IsActive,
                ["Notes"] = p.Notes
            });
        }

        return result;
    }

    private async Task<QueryResult> ExecuteBudgetQueryAsync(
        string userId,
        QueryDefinition query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.Budgets
            .Where(b => b.UserId == userId)
            .AsNoTracking();

        baseQuery = ApplyFilters(baseQuery, query.Filters);

        var result = new QueryResult
        {
            TotalCount = await baseQuery.CountAsync(cancellationToken)
        };

        var budgets = await baseQuery
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        result.Columns = GetBudgetColumns();

        foreach (var b in budgets)
        {
            result.Rows.Add(new Dictionary<string, object?>
            {
                ["Id"] = b.Id,
                ["Name"] = b.Name,
                ["Year"] = b.Year,
                ["Month"] = b.Month,
                ["IsDefault"] = b.IsDefault
            });
        }

        return result;
    }

    private static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, List<FilterCondition> filters)
    {
        foreach (var filter in filters)
        {
            query = ApplyFilter(query, filter);
        }
        return query;
    }

    private static IQueryable<T> ApplyFilter<T>(IQueryable<T> query, FilterCondition filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Property))
            return query;

        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = GetPropertyExpression(parameter, filter.Property);

            if (property == null)
                return query;

            var value = ConvertValue(filter.Value, filter.DataType, property.Type);
            var comparison = GetComparisonExpression(property, value, filter);

            if (comparison == null)
                return query;

            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            return query.Where(lambda);
        }
        catch
        {
            // If filter application fails, skip it
            return query;
        }
    }

    private static Expression? GetPropertyExpression(Expression parameter, string propertyPath)
    {
        try
        {
            Expression current = parameter;
            foreach (var part in propertyPath.Split('.'))
            {
                var type = current.Type;
                var prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null)
                    return null;
                current = Expression.Property(current, prop);
            }
            return current;
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertValue(string? value, PropertyDataType dataType, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return dataType switch
        {
            PropertyDataType.Integer => int.TryParse(value, out var i) ? i : null,
            PropertyDataType.Decimal => decimal.TryParse(value, out var d) ? d : null,
            PropertyDataType.DateTime => DateTimeOffset.TryParse(value, out var dt) ? dt : null,
            PropertyDataType.Boolean => bool.TryParse(value, out var b) ? b : null,
            PropertyDataType.Enum => underlyingType.IsEnum ? Enum.Parse(underlyingType, value, true) : value,
            _ => value
        };
    }

    private static Expression? GetComparisonExpression(Expression property, object? value, FilterCondition filter)
    {
        if (filter.Operator is FilterOperator.IsNull or FilterOperator.IsNotNull)
        {
            var nullConst = Expression.Constant(null, property.Type);
            return filter.Operator == FilterOperator.IsNull
                ? Expression.Equal(property, nullConst)
                : Expression.NotEqual(property, nullConst);
        }

        if (value == null)
            return null;

        var constant = Expression.Constant(value, property.Type);

        return filter.Operator switch
        {
            FilterOperator.Equals => Expression.Equal(property, constant),
            FilterOperator.NotEquals => Expression.NotEqual(property, constant),
            FilterOperator.GreaterThan => Expression.GreaterThan(property, constant),
            FilterOperator.LessThan => Expression.LessThan(property, constant),
            FilterOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(property, constant),
            FilterOperator.LessOrEqual => Expression.LessThanOrEqual(property, constant),
            FilterOperator.Contains => BuildStringContainsExpression(property, value.ToString()!),
            FilterOperator.StartsWith => BuildStringMethodExpression(property, "StartsWith", value.ToString()!),
            FilterOperator.EndsWith => BuildStringMethodExpression(property, "EndsWith", value.ToString()!),
            FilterOperator.Between => BuildBetweenExpression(property, value, filter.SecondValue, filter.DataType),
            _ => null
        };
    }

    private static Expression? BuildStringContainsExpression(Expression property, string value)
    {
        if (property.Type != typeof(string))
            return null;

        var method = typeof(string).GetMethod("Contains", [typeof(string)])!;
        return Expression.Call(property, method, Expression.Constant(value));
    }

    private static Expression? BuildStringMethodExpression(Expression property, string methodName, string value)
    {
        if (property.Type != typeof(string))
            return null;

        var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
        return Expression.Call(property, method, Expression.Constant(value));
    }

    private static Expression? BuildBetweenExpression(Expression property, object value1, string? value2Str, PropertyDataType dataType)
    {
        if (string.IsNullOrEmpty(value2Str))
            return null;

        var value2 = ConvertValue(value2Str, dataType, property.Type);
        if (value2 == null)
            return null;

        var const1 = Expression.Constant(value1, property.Type);
        var const2 = Expression.Constant(value2, property.Type);

        var gte = Expression.GreaterThanOrEqual(property, const1);
        var lte = Expression.LessThanOrEqual(property, const2);

        return Expression.AndAlso(gte, lte);
    }

    private static IQueryable<T> ApplySort<T>(IQueryable<T> query, SortConfig sort)
    {
        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = GetPropertyExpression(parameter, sort.Property);

            if (property == null)
                return query;

            var lambda = Expression.Lambda(property, parameter);
            var methodName = sort.Ascending ? "OrderBy" : "OrderByDescending";

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
        }
        catch
        {
            return query;
        }
    }

    private static List<ColumnMetadata> GetTransactionColumns(List<string>? displayColumns = null)
    {
        var allColumns = new List<ColumnMetadata>
        {
            new() { Name = "Date", DisplayName = "Date", DataType = PropertyDataType.DateTime },
            new() { Name = "Amount", DisplayName = "Amount", DataType = PropertyDataType.Decimal },
            new() { Name = "Payee", DisplayName = "Payee", DataType = PropertyDataType.String },
            new() { Name = "Category", DisplayName = "Category", DataType = PropertyDataType.String },
            new() { Name = "CategoryGroup", DisplayName = "Category Group", DataType = PropertyDataType.String },
            new() { Name = "Account", DisplayName = "Account", DataType = PropertyDataType.String },
            new() { Name = "Memo", DisplayName = "Memo", DataType = PropertyDataType.String },
            new() { Name = "Status", DisplayName = "Status", DataType = PropertyDataType.Enum },
            new() { Name = "IsCleared", DisplayName = "Cleared", DataType = PropertyDataType.Boolean },
            new() { Name = "IsReconciled", DisplayName = "Reconciled", DataType = PropertyDataType.Boolean }
        };

        if (displayColumns == null || displayColumns.Count == 0)
            return allColumns;

        return allColumns.Where(c => displayColumns.Contains(c.Name)).ToList();
    }

    private static List<ColumnMetadata> GetAccountColumns() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "Type", DisplayName = "Type", DataType = PropertyDataType.Enum },
        new() { Name = "SubType", DisplayName = "Sub-Type", DataType = PropertyDataType.Enum },
        new() { Name = "Group", DisplayName = "Group", DataType = PropertyDataType.String },
        new() { Name = "OpeningBalance", DisplayName = "Opening Balance", DataType = PropertyDataType.Decimal },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean },
        new() { Name = "CurrencyCode", DisplayName = "Currency", DataType = PropertyDataType.String }
    ];

    private static List<ColumnMetadata> GetCategoryColumns() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "GroupName", DisplayName = "Group", DataType = PropertyDataType.String },
        new() { Name = "GroupType", DisplayName = "Type", DataType = PropertyDataType.Enum },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean },
        new() { Name = "SortOrder", DisplayName = "Sort Order", DataType = PropertyDataType.Integer }
    ];

    private static List<ColumnMetadata> GetPayeeColumns() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "DefaultCategory", DisplayName = "Default Category", DataType = PropertyDataType.String },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean },
        new() { Name = "Notes", DisplayName = "Notes", DataType = PropertyDataType.String }
    ];

    private static List<ColumnMetadata> GetBudgetColumns() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "Year", DisplayName = "Year", DataType = PropertyDataType.Integer },
        new() { Name = "Month", DisplayName = "Month", DataType = PropertyDataType.Integer },
        new() { Name = "IsDefault", DisplayName = "Is Default", DataType = PropertyDataType.Boolean }
    ];

    private static string GetDisplayName(string propertyPath)
    {
        return propertyPath switch
        {
            "Category.Name" => "Category",
            "Category.CategoryGroup.Name" => "Category Group",
            "Account.Name" => "Account",
            "Payee.Name" => "Payee",
            _ => propertyPath.Split('.').Last()
        };
    }

    public async Task<SavedQuery> SaveQueryAsync(
        string userId,
        string name,
        string? description,
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        var savedQuery = new SavedQuery
        {
            UserId = userId,
            Name = name,
            Description = description,
            QueryDefinitionJson = SerializeQuery(query),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.SavedQueries.Add(savedQuery);
        await context.SaveChangesAsync(cancellationToken);

        return savedQuery;
    }

    public async Task<bool> UpdateQueryAsync(
        int queryId,
        string userId,
        string name,
        string? description,
        QueryDefinition query,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.SavedQueries
            .FirstOrDefaultAsync(q => q.Id == queryId && q.UserId == userId, cancellationToken);

        if (existing == null) return false;

        existing.Name = name;
        existing.Description = description;
        existing.QueryDefinitionJson = SerializeQuery(query);
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<SavedQuery>> GetSavedQueriesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await context.SavedQueries
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<SavedQuery?> LoadQueryAsync(
        int queryId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await context.SavedQueries
            .FirstOrDefaultAsync(q => q.Id == queryId && q.UserId == userId, cancellationToken);
    }

    public async Task<bool> DeleteQueryAsync(
        int queryId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var query = await context.SavedQueries
            .FirstOrDefaultAsync(q => q.Id == queryId && q.UserId == userId, cancellationToken);

        if (query == null) return false;

        context.SavedQueries.Remove(query);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public List<PropertyMetadata> GetEntityProperties(QueryTargetEntity entity)
    {
        return entity switch
        {
            QueryTargetEntity.Transaction => GetTransactionProperties(),
            QueryTargetEntity.Account => GetAccountProperties(),
            QueryTargetEntity.Category => GetCategoryProperties(),
            QueryTargetEntity.Payee => GetPayeeProperties(),
            QueryTargetEntity.Budget => GetBudgetProperties(),
            _ => []
        };
    }

    public QueryValidationResult ValidateQuery(QueryDefinition query)
    {
        var result = new QueryValidationResult { IsValid = true };

        // Validate filters
        foreach (var filter in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Property))
            {
                result.Errors.Add("Filter property cannot be empty");
                result.IsValid = false;
            }
        }

        // Validate aggregation
        if (query.Aggregation != null && string.IsNullOrWhiteSpace(query.Aggregation.Property))
        {
            result.Errors.Add("Aggregation property is required");
            result.IsValid = false;
        }

        // Validate chart config
        if (query.Chart != null)
        {
            if (string.IsNullOrWhiteSpace(query.Chart.XAxisProperty))
            {
                result.Errors.Add("Chart X-axis property is required");
                result.IsValid = false;
            }
            if (string.IsNullOrWhiteSpace(query.Chart.YAxisProperty))
            {
                result.Errors.Add("Chart Y-axis property is required");
                result.IsValid = false;
            }
        }

        return result;
    }

    public QueryDefinition? DeserializeQuery(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<QueryDefinition>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public string SerializeQuery(QueryDefinition query)
    {
        return JsonSerializer.Serialize(query, JsonOptions);
    }

    private static List<PropertyMetadata> GetTransactionProperties() =>
    [
        new() { Name = "Date", DisplayName = "Date", DataType = PropertyDataType.DateTime, IsGroupable = true },
        new() { Name = "Amount", DisplayName = "Amount", DataType = PropertyDataType.Decimal, IsAggregatable = true },
        new() { Name = "Payee.Name", DisplayName = "Payee", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "Category.Name", DisplayName = "Category", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "Category.CategoryGroup.Name", DisplayName = "Category Group", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "Account.Name", DisplayName = "Account", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "Memo", DisplayName = "Memo", DataType = PropertyDataType.String },
        new() { Name = "ReferenceNumber", DisplayName = "Reference #", DataType = PropertyDataType.String },
        new() { Name = "Tags", DisplayName = "Tags", DataType = PropertyDataType.String },
        new() { Name = "Status", DisplayName = "Status", DataType = PropertyDataType.Enum, EnumValues = ["Pending", "Posted"], IsGroupable = true },
        new() { Name = "IsCleared", DisplayName = "Cleared", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "IsReconciled", DisplayName = "Reconciled", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "IsRecurring", DisplayName = "Recurring", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "CreatedAt", DisplayName = "Created", DataType = PropertyDataType.DateTime },
        new() { Name = "UpdatedAt", DisplayName = "Updated", DataType = PropertyDataType.DateTime }
    ];

    private static List<PropertyMetadata> GetAccountProperties() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "Type", DisplayName = "Type", DataType = PropertyDataType.Enum, EnumValues = ["Asset", "Liability"], IsGroupable = true },
        new() { Name = "SubType", DisplayName = "Sub-Type", DataType = PropertyDataType.Enum, IsGroupable = true },
        new() { Name = "Group", DisplayName = "Group", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "OpeningBalance", DisplayName = "Opening Balance", DataType = PropertyDataType.Decimal, IsAggregatable = true },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "CurrencyCode", DisplayName = "Currency", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "MonthlySpendingLimit", DisplayName = "Monthly Limit", DataType = PropertyDataType.Decimal, IsAggregatable = true },
        new() { Name = "CreatedAt", DisplayName = "Created", DataType = PropertyDataType.DateTime }
    ];

    private static List<PropertyMetadata> GetCategoryProperties() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "CategoryGroup.Name", DisplayName = "Group", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "CategoryGroup.Type", DisplayName = "Type", DataType = PropertyDataType.Enum, EnumValues = ["Income", "Expense"], IsGroupable = true },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "SortOrder", DisplayName = "Sort Order", DataType = PropertyDataType.Integer }
    ];

    private static List<PropertyMetadata> GetPayeeProperties() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "DefaultCategory.Name", DisplayName = "Default Category", DataType = PropertyDataType.String, IsGroupable = true },
        new() { Name = "IsActive", DisplayName = "Active", DataType = PropertyDataType.Boolean, IsGroupable = true },
        new() { Name = "Notes", DisplayName = "Notes", DataType = PropertyDataType.String }
    ];

    private static List<PropertyMetadata> GetBudgetProperties() =>
    [
        new() { Name = "Name", DisplayName = "Name", DataType = PropertyDataType.String },
        new() { Name = "Year", DisplayName = "Year", DataType = PropertyDataType.Integer, IsGroupable = true },
        new() { Name = "Month", DisplayName = "Month", DataType = PropertyDataType.Integer, IsGroupable = true },
        new() { Name = "IsDefault", DisplayName = "Is Default", DataType = PropertyDataType.Boolean, IsGroupable = true }
    ];
}

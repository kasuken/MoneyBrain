using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using MoneyBrain.Web.Application.InsightExplorer;
using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Data;

namespace MoneyBrain.Web.Application.Mcp.Tools;

/// <summary>Exposes bounded saved-query management and allowlisted Insight Explorer execution.</summary>
[McpServerToolType]
public sealed class SavedQueryTools(
    McpAccess access,
    IInsightExplorerService explorer,
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    /// <summary>Lists saved-query definitions without navigation properties or user identifiers.</summary>
    [McpServerTool(Name = "saved_queries_list", ReadOnly = true), Description("List the current user's saved Insight Explorer queries, with server-side pagination.")]
    public async Task<List<SavedQueryView>> ListAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireRead();
        ReportInput.Page(page, pageSize);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavedQueries.AsNoTracking().Where(q => q.UserId == userId)
            .OrderBy(q => q.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(q => new SavedQueryView(q.Id, q.Name, q.Description, q.QueryDefinitionJson, q.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Gets the field allowlist for a supported entity.</summary>
    [McpServerTool(Name = "query_properties", ReadOnly = true), Description("Discover supported Insight Explorer property paths and types. Only these paths may be used in MCP filters, sorting, or aggregation.")]
    public List<PropertyMetadata> Properties(QueryTargetEntity entity)
    {
        access.RequireRead();
        if (!Enum.IsDefined(entity)) throw ReportInput.Error("InvalidQuery");
        return explorer.GetEntityProperties(entity);
    }

    /// <summary>Executes a validated query within MCP row and source-record limits.</summary>
    [McpServerTool(Name = "query_execute", ReadOnly = true), Description("Execute a typed Insight Explorer query, scoped to the current user. Max 100 rows/page, 20 filters, 10000 source records for aggregation, and 200 aggregate groups. Property paths must come from query_properties. Only transactions support aggregation (Amount).")]
    public Task<QueryResult> ExecuteAsync(QueryDefinition definition, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireRead();
        return ExecuteCoreAsync(userId, definition, page, pageSize, cancellationToken);
    }

    /// <summary>Executes an owned saved query after revalidating its stored definition.</summary>
    [McpServerTool(Name = "saved_query_execute", ReadOnly = true), Description("Execute one of the current user's saved queries with the same validation and size limits as query_execute.")]
    public async Task<QueryResult> ExecuteSavedAsync(int queryId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireRead();
        var saved = await explorer.LoadQueryAsync(queryId, userId, cancellationToken)
            ?? throw ReportInput.Error("NotFound");
        var definition = explorer.DeserializeQuery(saved.QueryDefinitionJson)
            ?? throw ReportInput.Error("InvalidQuery");
        return await ExecuteCoreAsync(userId, definition, page, pageSize, cancellationToken);
    }

    /// <summary>Saves a validated query definition.</summary>
    [McpServerTool(Name = "saved_query_create"), Description("Save an Insight Explorer query for the current user. Requires write scope, instance write enablement, and confirmed=true after user approval.")]
    public async Task<SavedQueryView> CreateAsync(string name, QueryDefinition definition, bool confirmed = false, string? description = null, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireWrite(confirmed);
        ValidateName(name, description);
        Validate(definition);
        var saved = await explorer.SaveQueryAsync(userId, name, description, definition, cancellationToken);
        return new(saved.Id, saved.Name, saved.Description, saved.QueryDefinitionJson, saved.UpdatedAt);
    }

    /// <summary>A saved-query definition without persistence or identity navigation properties.</summary>
    /// <param name="Id">The query identifier.</param>
    /// <param name="Name">The user-assigned name.</param>
    /// <param name="Description">The optional description.</param>
    /// <param name="Definition">The serialized Insight Explorer definition.</param>
    /// <param name="UpdatedAt">The UTC modification timestamp.</param>
    public sealed record SavedQueryView(int Id, string Name, string? Description, string Definition, DateTime UpdatedAt);

    /// <summary>Updates an owned saved query.</summary>
    [McpServerTool(Name = "saved_query_update"), Description("Update an owned saved query. Requires write scope, enabled writes and confirmed=true after user approval.")]
    public async Task<bool> UpdateAsync(int queryId, string name, QueryDefinition definition, bool confirmed = false, string? description = null, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireWrite(confirmed);
        ValidateName(name, description);
        Validate(definition);
        if (!await explorer.UpdateQueryAsync(queryId, userId, name, description, definition, cancellationToken))
            throw ReportInput.Error("NotFound");
        return true;
    }

    /// <summary>Deletes an owned saved query definition, not the underlying financial records.</summary>
    [McpServerTool(Name = "saved_query_delete"), Description("Delete a saved query, not its data. Read its definition first and obtain explicit user approval; requires confirmed=true and write access.")]
    public async Task<bool> DeleteAsync(int queryId, bool confirmed = false, CancellationToken cancellationToken = default)
    {
        var userId = access.RequireWrite(confirmed);
        if (!await explorer.DeleteQueryAsync(queryId, userId, cancellationToken))
            throw ReportInput.Error("NotFound");
        return true;
    }

    private async Task<QueryResult> ExecuteCoreAsync(string userId, QueryDefinition definition, int page, int pageSize, CancellationToken cancellationToken)
    {
        ReportInput.Page(page, pageSize);
        Validate(definition);
        if (definition.Aggregation is not null)
        {
            var probe = await explorer.ExecuteQueryAsync(userId, new QueryDefinition
            {
                TargetEntity = definition.TargetEntity,
                Filters = definition.Filters
            }, 1, 1, cancellationToken);
            if (probe.TotalCount > 10000) throw ReportInput.Error("QueryTooLarge");
            if (probe.TotalCount == 0)
                return new QueryResult { TotalCount = 0 };
        }

        var result = await explorer.ExecuteQueryAsync(userId, definition, page, pageSize, cancellationToken);
        if (result.AggregatedRows.Count > 200) throw ReportInput.Error("QueryTooLarge");
        return result;
    }

    private void Validate(QueryDefinition definition)
    {
        if (definition is null || !Enum.IsDefined(definition.TargetEntity) ||
            definition.Filters is null || definition.Filters.Count > 20 ||
            definition.DisplayColumns is null || definition.DisplayColumns.Count > 30)
            throw ReportInput.Error("InvalidQuery");

        var properties = explorer.GetEntityProperties(definition.TargetEntity)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var filter in definition.Filters)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Property) ||
                !properties.TryGetValue(filter.Property, out var property) ||
                !property.IsFilterable || property.DataType != filter.DataType ||
                !Enum.IsDefined(filter.Operator) || filter.Operator == FilterOperator.InList ||
                !ValidOperator(property.DataType, filter.Operator) ||
                filter.Value?.Length > 1000 || filter.SecondValue?.Length > 1000 ||
                !ValidValue(property, filter.Value, filter.Operator) ||
                (filter.Operator == FilterOperator.Between && !ValidValue(property, filter.SecondValue, filter.Operator)))
                throw ReportInput.Error("InvalidQuery");
        }

        if (definition.Sort is { } sort &&
            (string.IsNullOrWhiteSpace(sort.Property) || !properties.ContainsKey(sort.Property)))
            throw ReportInput.Error("InvalidQuery");
        if (definition.Aggregation is { } aggregation &&
            (definition.TargetEntity != QueryTargetEntity.Transaction || aggregation.Property != "Amount" ||
             !Enum.IsDefined(aggregation.Function) || aggregation.GroupBy is null || aggregation.GroupBy.Count > 1 ||
             aggregation.GroupBy.Any(p => p is not ("Category.Name" or "Category.CategoryGroup.Name" or "Account.Name" or "Payee.Name" or "Status" or "Date"))))
            throw ReportInput.Error("InvalidQuery");
        if (!explorer.ValidateQuery(definition).IsValid)
            throw ReportInput.Error("InvalidQuery");
    }

    private static bool ValidOperator(PropertyDataType type, FilterOperator op) => op switch
    {
        FilterOperator.Contains or FilterOperator.StartsWith or FilterOperator.EndsWith => type == PropertyDataType.String,
        FilterOperator.GreaterThan or FilterOperator.GreaterOrEqual or FilterOperator.LessThan or FilterOperator.LessOrEqual or FilterOperator.Between =>
            type is PropertyDataType.Integer or PropertyDataType.Decimal or PropertyDataType.DateTime,
        FilterOperator.IsNull or FilterOperator.IsNotNull => type == PropertyDataType.String,
        _ => op is FilterOperator.Equals or FilterOperator.NotEquals
    };

    private static bool ValidValue(PropertyMetadata property, string? value, FilterOperator op)
    {
        if (op is FilterOperator.IsNull or FilterOperator.IsNotNull) return true;
        if (string.IsNullOrEmpty(value)) return false;
        return property.DataType switch
        {
            PropertyDataType.Integer => int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out _),
            PropertyDataType.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out _),
            PropertyDataType.DateTime => DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out _),
            PropertyDataType.Boolean => bool.TryParse(value, out _),
            PropertyDataType.Enum => property.EnumValues?.Contains(value, StringComparer.OrdinalIgnoreCase) == true ||
                property.Name == "SubType" && Enum.TryParse<Domain.Enums.AccountSubType>(value, true, out var subType) && Enum.IsDefined(subType),
            _ => true
        };
    }

    private static void ValidateName(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || description?.Length > 2000)
            throw ReportInput.Error("InvalidName");
    }
}

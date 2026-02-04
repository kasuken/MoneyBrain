using MoneyBrain.Web.Application.InsightExplorer.Models;
using MoneyBrain.Web.Domain.Entities;

namespace MoneyBrain.Web.Application.InsightExplorer;

/// <summary>
/// Service for building and executing dynamic queries in the Insight Explorer
/// </summary>
public interface IInsightExplorerService
{
    /// <summary>
    /// Execute a query definition and return results
    /// </summary>
    Task<QueryResult> ExecuteQueryAsync(
        string userId,
        QueryDefinition query,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save a query for later reuse
    /// </summary>
    Task<SavedQuery> SaveQueryAsync(
        string userId,
        string name,
        string? description,
        QueryDefinition query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing saved query
    /// </summary>
    Task<bool> UpdateQueryAsync(
        int queryId,
        string userId,
        string name,
        string? description,
        QueryDefinition query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all saved queries for a user
    /// </summary>
    Task<List<SavedQuery>> GetSavedQueriesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a saved query by ID
    /// </summary>
    Task<SavedQuery?> LoadQueryAsync(
        int queryId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a saved query
    /// </summary>
    Task<bool> DeleteQueryAsync(
        int queryId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available properties for a target entity (for query builder UI)
    /// </summary>
    List<PropertyMetadata> GetEntityProperties(QueryTargetEntity entity);

    /// <summary>
    /// Validate a query definition before execution
    /// </summary>
    QueryValidationResult ValidateQuery(QueryDefinition query);

    /// <summary>
    /// Deserialize a query definition from JSON
    /// </summary>
    QueryDefinition? DeserializeQuery(string json);

    /// <summary>
    /// Serialize a query definition to JSON
    /// </summary>
    string SerializeQuery(QueryDefinition query);
}

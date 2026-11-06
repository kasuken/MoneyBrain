namespace MoneyBrain.Web.Application.Mcp;

/// <summary>Validated client and scopes displayed before a user authorizes access.</summary>
public sealed record McpConsentViewModel(
    string ClientName, IReadOnlyList<string> Scopes, IReadOnlyList<KeyValuePair<string, string>> Parameters);

/// <summary>A user's revocable OAuth authorization.</summary>
public sealed record McpGrantViewModel(string Id, string ClientName, IReadOnlyList<string> Scopes, DateTimeOffset? CreatedAt);

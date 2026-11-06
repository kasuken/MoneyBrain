using Microsoft.Extensions.Options;

namespace MoneyBrain.Web.Application.Mcp;

/// <summary>Opt-in settings for the local MCP resource and authorization server.</summary>
public sealed class McpSettings
{
    /// <summary>Whether the remote MCP integration is enabled.</summary>
    public bool Enabled { get; set; }
    /// <summary>Whether explicitly consented, confirmed mutations are enabled.</summary>
    public bool EnableWrites { get; set; }
    /// <summary>The externally visible HTTPS issuer origin, never inferred from request headers.</summary>
    public string PublicUrl { get; set; } = "";
    /// <summary>Preconfigured public OAuth clients; dynamic registration is not supported.</summary>
    public List<McpClientSettings> Clients { get; set; } = [];
    /// <summary>Path to the production signing PFX, supplied by deployment configuration.</summary>
    public string? SigningCertificatePath { get; set; }
    /// <summary>Password for the signing PFX, supplied through a secret provider.</summary>
    public string? SigningCertificatePassword { get; set; }
    /// <summary>Path to the production encryption PFX.</summary>
    public string? EncryptionCertificatePath { get; set; }
    /// <summary>Password for the encryption PFX, supplied through a secret provider.</summary>
    public string? EncryptionCertificatePassword { get; set; }
    /// <summary>The canonical protected resource identifier.</summary>
    public string Resource => PublicUrl.TrimEnd('/') + "/mcp";
}

/// <summary>A public client with exact, administrator-approved callback URIs.</summary>
public sealed class McpClientSettings
{
    /// <summary>The public client identifier.</summary>
    public string ClientId { get; set; } = "";
    /// <summary>A user-visible application name.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Exact redirect URIs, without wildcards or fragments.</summary>
    public List<string> RedirectUris { get; set; } = [];
    /// <summary>Browser origins permitted to call MCP; native clients need no Origin header.</summary>
    public List<string> AllowedOrigins { get; set; } = [];
    /// <summary>Whether this client may request the separate write scope.</summary>
    public bool AllowWrites { get; set; }
}

internal sealed class McpSettingsValidator(IHostEnvironment environment) : IValidateOptions<McpSettings>
{
    public ValidateOptionsResult Validate(string? name, McpSettings options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (!Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || issuer.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment) ||
            !string.IsNullOrEmpty(issuer.UserInfo))
            return ValidateOptionsResult.Fail("Mcp:PublicUrl must be an absolute HTTPS origin without a path, query, credentials or fragment.");

        if (!environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(options.SigningCertificatePath) ||
             string.IsNullOrWhiteSpace(options.EncryptionCertificatePath)))
            return ValidateOptionsResult.Fail("Production MCP requires signing and encryption certificate paths.");

        if (options.Clients.Count > 100)
            return ValidateOptionsResult.Fail("At most 100 MCP clients may be configured.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId) || client.ClientId.Length > 100 ||
                !ids.Add(client.ClientId) || string.IsNullOrWhiteSpace(client.DisplayName) ||
                client.DisplayName.Length > 200 || client.RedirectUris.Count is < 1 or > 10)
                return ValidateOptionsResult.Fail("MCP clients require unique bounded identifiers, display names and exact redirect URIs.");

            foreach (var redirect in client.RedirectUris)
            {
                if (!Uri.TryCreate(redirect, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                    uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || redirect.Contains('*', StringComparison.Ordinal))
                    return ValidateOptionsResult.Fail("MCP redirect URIs must use HTTPS (HTTP loopback callbacks are allowed), without credentials, fragments or wildcards.");
            }

            foreach (var origin in client.AllowedOrigins)
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
                    uri.GetLeftPart(UriPartial.Authority) != origin)
                    return ValidateOptionsResult.Fail("MCP allowed origins must be exact HTTP loopback or HTTPS origins.");
            }
        }
        return ValidateOptionsResult.Success;
    }
}

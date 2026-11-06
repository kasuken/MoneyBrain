using Microsoft.Extensions.Options;
using System.Security.Claims;
using ModelContextProtocol;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace MoneyBrain.Web.Application.Mcp;

/// <summary>Authorizes tool operations exclusively against a validated OAuth bearer identity.</summary>
public sealed class McpAccess(IHttpContextAccessor httpContextAccessor, IOptions<McpSettings> settings)
{
    /// <summary>The read-only financial data scope.</summary>
    public const string ReadScope = "moneybrain.read";
    /// <summary>The separately consented mutation scope.</summary>
    public const string WriteScope = "moneybrain.write";

    /// <summary>Requires a trusted bearer subject and read scope.</summary>
    /// <returns>The authenticated, server-issued subject user identifier.</returns>
    /// <exception cref="McpException">Access is unavailable or not authorized.</exception>
    public string RequireRead()
    {
        var user = BearerPrincipal();
        var subject = user?.GetClaim(OpenIddictConstants.Claims.Subject);
        if (!settings.Value.Enabled || user is null ||
            !user.HasScope(ReadScope) || string.IsNullOrWhiteSpace(subject))
            throw new McpException("MCP access requires an authorized read token.");

        return subject;
    }

    /// <summary>Requires enabled writes, the separate write scope and explicit confirmation.</summary>
    /// <param name="confirmed">Whether the caller explicitly confirmed this particular mutation.</param>
    /// <returns>The authenticated, server-issued subject user identifier.</returns>
    /// <exception cref="McpException">The mutation is not authorized or confirmed.</exception>
    public string RequireWrite(bool confirmed)
    {
        var userId = RequireRead();
        if (!settings.Value.EnableWrites ||
            BearerPrincipal()?.HasScope(WriteScope) != true || !confirmed)
            throw new McpException("MCP writes require enabled write access and explicit confirmation.");
        return userId;
    }

    private ClaimsPrincipal? BearerPrincipal()
    {
        var identity = httpContextAccessor.HttpContext?.User.Identities.FirstOrDefault(identity =>
            identity.IsAuthenticated &&
            identity.AuthenticationType == OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        // Never combine a browser identity's subject/scopes with an unrelated bearer identity.
        return identity is null ? null : new ClaimsPrincipal(identity);
    }
}

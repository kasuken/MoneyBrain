using Microsoft.Extensions.Options;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MoneyBrain.Web.Application.Mcp;

/// <summary>Synchronizes administrator-configured public clients into the local OAuth store.</summary>
public sealed class McpClientProvisioner(
    IOpenIddictApplicationManager applications,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens,
    IOptions<McpSettings> settings)
{
    /// <summary>Persistent application property identifying clients owned by this integration.</summary>
    public const string ManagedClientProperty = "moneybrain.mcp.managed";

    /// <summary>Creates approved clients and revokes grants for removed or changed client permissions.</summary>
    /// <param name="cancellationToken">Cancellation for database operations.</param>
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var configuration = settings.Value;
        if (!configuration.Enabled)
            return;

        var storedApplications = new List<object>();
        await foreach (var application in applications.ListAsync(cancellationToken: cancellationToken))
            storedApplications.Add(application);
        // Check all collisions before changing any persisted clients or grants. An existing
        // application without our marker belongs to another integration, even if IDs match.
        foreach (var existing in storedApplications)
        {
            var clientId = await applications.GetClientIdAsync(existing, cancellationToken);
            if (configuration.Clients.Any(client => client.ClientId == clientId) &&
                !await IsManagedAsync(existing, cancellationToken))
                throw new InvalidOperationException("A configured MCP client ID conflicts with an application managed outside this integration.");
        }
        foreach (var existing in storedApplications)
        {
            if (!await IsManagedAsync(existing, cancellationToken))
                continue;
            var clientId = await applications.GetClientIdAsync(existing, cancellationToken);
            if (configuration.Clients.Any(client => client.ClientId == clientId))
                continue;
            var id = await applications.GetIdAsync(existing, cancellationToken);
            if (id is not null)
            {
                await authorizations.RevokeByApplicationIdAsync(id, cancellationToken);
                await tokens.RevokeByApplicationIdAsync(id, cancellationToken);
            }
            await applications.DeleteAsync(existing, cancellationToken);
        }

        foreach (var client in configuration.Clients)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                DisplayName = client.DisplayName,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Explicit
            };
            descriptor.Properties[ManagedClientProperty] = JsonSerializer.SerializeToElement(true);
            foreach (var uri in client.RedirectUris)
                descriptor.RedirectUris.Add(new Uri(uri));
            descriptor.Permissions.UnionWith([
                Permissions.Endpoints.Authorization, Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + McpAccess.ReadScope
            ]);
            if (configuration.EnableWrites && client.AllowWrites)
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + McpAccess.WriteScope);
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
            descriptor.AddResourcePermissions(configuration.Resource);

            var application = await applications.FindByClientIdAsync(client.ClientId, cancellationToken);
            if (application is null)
            {
                await applications.CreateAsync(descriptor, cancellationToken);
                continue;
            }
            var previous = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(previous, application, cancellationToken);
            // Also guard the direct lookup in case another administrator created a colliding
            // application after the initial reconciliation snapshot.
            if (!await IsManagedAsync(application, cancellationToken))
                throw new InvalidOperationException("A configured MCP client ID conflicts with an application managed outside this integration.");
            if (previous.RedirectUris.SetEquals(descriptor.RedirectUris) &&
                previous.Permissions.SetEquals(descriptor.Permissions) &&
                previous.Requirements.SetEquals(descriptor.Requirements) &&
                previous.ClientType == descriptor.ClientType &&
                previous.ConsentType == descriptor.ConsentType &&
                previous.DisplayName == descriptor.DisplayName)
                continue;

            // Tightened configuration must invalidate already issued credentials, not just future consent.
            var applicationId = await applications.GetIdAsync(application, cancellationToken);
            if (applicationId is not null)
            {
                await authorizations.RevokeByApplicationIdAsync(applicationId, cancellationToken);
                await tokens.RevokeByApplicationIdAsync(applicationId, cancellationToken);
            }
            await applications.UpdateAsync(application, descriptor, cancellationToken);
        }
    }

    private async ValueTask<bool> IsManagedAsync(object application, CancellationToken cancellationToken)
    {
        var properties = await applications.GetPropertiesAsync(application, cancellationToken);
        return properties.TryGetValue(ManagedClientProperty, out var marker) && marker.ValueKind == JsonValueKind.True;
    }
}

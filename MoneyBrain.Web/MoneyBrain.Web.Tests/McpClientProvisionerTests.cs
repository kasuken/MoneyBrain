using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Mcp;
using MoneyBrain.Web.Tests.Infrastructure;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MoneyBrain.Web.Tests;

/// <summary>Exercises configured-client synchronization against the real OpenIddict EF stores.</summary>
[TestClass]
public sealed class McpClientProvisionerTests
{
    /// <summary>External migration management must not disable administrator-configured client provisioning.</summary>
    [TestMethod]
    public async Task Startup_provisions_configured_client_when_database_migrations_are_disabled()
    {
        await using var factory = new MoneyBrainFactory(configureClient: true);
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await applications.FindByClientIdAsync(OAuthFlow.ClientId);

        Assert.IsNotNull(application);
        var properties = await applications.GetPropertiesAsync(application);
        Assert.AreEqual(JsonValueKind.True, properties[McpClientProvisioner.ManagedClientProperty].ValueKind);
        CollectionAssert.Contains((await applications.GetRedirectUrisAsync(application)).ToArray(), OAuthFlow.RedirectUri);
        CollectionAssert.Contains((await applications.GetPermissionsAsync(application)).ToArray(),
            Permissions.Prefixes.Scope + McpAccess.ReadScope);
    }

    /// <summary>Synchronization removes managed clients only; unrelated OAuth registrations are preserved.</summary>
    [TestMethod]
    public async Task Removed_managed_client_is_deleted_but_unrelated_application_is_untouched()
    {
        await using var factory = new MoneyBrainFactory();
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await applications.CreateAsync(Descriptor("removed-managed", managed: true));
        var unrelated = await applications.CreateAsync(Descriptor("unrelated", managed: false));
        var originalId = await applications.GetIdAsync(unrelated);

        await Provisioner(scope.ServiceProvider, Settings()).ProvisionAsync();

        Assert.IsNull(await applications.FindByClientIdAsync("removed-managed"));
        var preserved = await applications.FindByClientIdAsync("unrelated");
        Assert.IsNotNull(preserved);
        Assert.AreEqual(originalId, await applications.GetIdAsync(preserved));
        Assert.IsFalse((await applications.GetPropertiesAsync(preserved)).ContainsKey(McpClientProvisioner.ManagedClientProperty));
    }

    /// <summary>A configured identifier collision fails before deleting other managed registrations.</summary>
    [TestMethod]
    public async Task Unmanaged_identifier_collision_fails_without_partial_reconciliation()
    {
        await using var factory = new MoneyBrainFactory();
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var existing = await applications.CreateAsync(Descriptor(OAuthFlow.ClientId, managed: false));
        await applications.CreateAsync(Descriptor("would-be-removed", managed: true));
        var originalName = await applications.GetDisplayNameAsync(existing);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            Provisioner(scope.ServiceProvider, Settings(ConfiguredClient())).ProvisionAsync());

        var preserved = await applications.FindByClientIdAsync(OAuthFlow.ClientId);
        Assert.IsNotNull(preserved);
        Assert.AreEqual(originalName, await applications.GetDisplayNameAsync(preserved));
        Assert.IsFalse((await applications.GetPropertiesAsync(preserved)).ContainsKey(McpClientProvisioner.ManagedClientProperty));
        Assert.IsNotNull(await applications.FindByClientIdAsync("would-be-removed"));
    }

    /// <summary>Repeated identical provisioning retains the application identifier and its managed marker.</summary>
    [TestMethod]
    public async Task Unchanged_configured_client_is_not_deleted_and_recreated()
    {
        await using var factory = new MoneyBrainFactory();
        using var scope = factory.Services.CreateScope();
        var settings = Settings(ConfiguredClient());
        var provisioner = Provisioner(scope.ServiceProvider, settings);
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await provisioner.ProvisionAsync();
        var original = await applications.FindByClientIdAsync(OAuthFlow.ClientId);
        Assert.IsNotNull(original);
        var originalId = await applications.GetIdAsync(original);

        await provisioner.ProvisionAsync();

        var current = await applications.FindByClientIdAsync(OAuthFlow.ClientId);
        Assert.IsNotNull(current);
        Assert.AreEqual(originalId, await applications.GetIdAsync(current));
    }

    private static McpClientProvisioner Provisioner(IServiceProvider services, McpSettings settings) => new(
        services.GetRequiredService<IOpenIddictApplicationManager>(),
        services.GetRequiredService<IOpenIddictAuthorizationManager>(),
        services.GetRequiredService<IOpenIddictTokenManager>(), Options.Create(settings));

    private static McpSettings Settings(params McpClientSettings[] clients) => new()
    {
        Enabled = true, PublicUrl = "https://localhost", Clients = clients.ToList()
    };

    private static McpClientSettings ConfiguredClient() => new()
    {
        ClientId = OAuthFlow.ClientId, DisplayName = "Approved test client",
        RedirectUris = [OAuthFlow.RedirectUri]
    };

    private static OpenIddictApplicationDescriptor Descriptor(string clientId, bool managed)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId, DisplayName = "Original " + clientId,
            ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Explicit
        };
        descriptor.RedirectUris.Add(new Uri(OAuthFlow.RedirectUri));
        descriptor.Permissions.UnionWith([
            Permissions.Endpoints.Authorization, Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + McpAccess.ReadScope
        ]);
        if (managed)
            descriptor.Properties[McpClientProvisioner.ManagedClientProperty] = JsonSerializer.SerializeToElement(true);
        return descriptor;
    }
}

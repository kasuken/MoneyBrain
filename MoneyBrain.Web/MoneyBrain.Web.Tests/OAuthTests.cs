using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Tests.Infrastructure;
using MoneyBrain.Web.Data;
using MoneyBrain.Web.Application.Mcp;
using OpenIddict.Abstractions;

namespace MoneyBrain.Web.Tests;

/// <summary>Exercises the actual Identity login, explicit consent and OpenIddict token endpoints.</summary>
[TestClass]
public sealed class OAuthTests
{
    /// <summary>Tokens obtained through authorization code and S256 PKCE initialize MCP successfully.</summary>
    [TestMethod]
    public async Task Authorization_code_with_PKCE_issues_token_accepted_by_Mcp()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        var token = await OAuthFlow.IssueTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await McpHttpTests.SendAsync(client, "initialize", new
        {
            protocolVersion = "2025-06-18", capabilities = new { },
            clientInfo = new { name = "MoneyBrain integration tests", version = "1" }
        });
        using var rpc = await McpHttpTests.ReadRpcAsync(response);

        Assert.AreEqual("MoneyBrain", rpc.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
    }

    /// <summary>Bearer protection does not accept a valid Identity browser login cookie as an API token.</summary>
    [TestMethod]
    public async Task Identity_cookie_alone_does_not_authorize_Mcp()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);

        using var response = await McpHttpTests.SendAsync(client, "tools/list");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Consent requires antiforgery validation even for an authenticated browser.</summary>
    [TestMethod]
    public async Task Consent_without_antiforgery_is_rejected()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);
        var form = OAuthFlow.AuthorizationParameters().ToDictionary(pair => pair.Key, pair => pair.Value ?? "");
        form["decision"] = "accept";

        using var response = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(form));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Unregistered redirect URIs, unsupported scopes and missing PKCE are rejected without redirecting secrets.</summary>
    [TestMethod]
    [DataRow("redirect_uri", "https://attacker.example.test/callback")]
    [DataRow("scope", "moneybrain.read moneybrain.write")]
    [DataRow("scope", "openid")]
    [DataRow("code_challenge", null)]
    [DataRow("code_challenge_method", "plain")]
    [DataRow("resource", "https://attacker.example.test/mcp")]
    public async Task Invalid_authorization_request_is_rejected(string parameter, string? value)
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        var parameters = OAuthFlow.AuthorizationParameters();
        if (value is null)
            parameters.Remove(parameter);
        else
            parameters[parameter] = value;

        using var response = await client.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", parameters));

        Assert.IsTrue(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Found,
            await response.Content.ReadAsStringAsync());
        if (response.Headers.Location is { } location)
        {
            Assert.AreEqual(new Uri(OAuthFlow.RedirectUri).Host, location.Host);
            Assert.IsTrue(QueryHelpers.ParseQuery(location.Query).ContainsKey("error"), location.ToString());
            Assert.IsFalse(QueryHelpers.ParseQuery(location.Query).ContainsKey("code"));
        }
    }

    /// <summary>Authorization codes cannot be exchanged with a different PKCE verifier.</summary>
    [TestMethod]
    public async Task Incorrect_PKCE_verifier_is_rejected()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);
        var code = await OAuthFlow.AuthorizeAsync(client);

        using var response = await OAuthFlow.ExchangeAsync(client, code, new string('b', 64));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("invalid_grant", payload.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Token requests must preserve the resource and redirect URI bound to the code.</summary>
    [TestMethod]
    [DataRow(null, OAuthFlow.RedirectUri)]
    [DataRow("https://attacker.example.test/mcp", OAuthFlow.RedirectUri)]
    [DataRow(OAuthFlow.Resource, "https://client.example.test/other")]
    public async Task Token_exchange_rejects_resource_or_redirect_mismatch(string? resource, string redirectUri)
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);
        var code = await OAuthFlow.AuthorizeAsync(client);

        using var response = await OAuthFlow.ExchangeAsync(client, code, resource: resource, redirectUri: redirectUri);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsTrue(payload.RootElement.TryGetProperty("error", out _));
        Assert.IsFalse(payload.RootElement.TryGetProperty("access_token", out _));
    }

    /// <summary>A redeemed code cannot mint another access token.</summary>
    [TestMethod]
    public async Task Authorization_code_is_single_use()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);
        var code = await OAuthFlow.AuthorizeAsync(client);
        using var first = await OAuthFlow.ExchangeAsync(client, code);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode, await first.Content.ReadAsStringAsync());

        using var replay = await OAuthFlow.ExchangeAsync(client, code);
        using var payload = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.AreEqual("invalid_grant", payload.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Revoking a browser-owned grant immediately prevents further protocol calls with its bearer token.</summary>
    [TestMethod]
    public async Task Revoking_grant_invalidates_existing_access_token()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var browser = factory.CreateHttpsClient();
        var token = await OAuthFlow.IssueTokenAsync(browser);
        using var bearer = factory.CreateHttpsClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var before = await McpHttpTests.SendAsync(bearer, "tools/list");
        Assert.AreEqual(HttpStatusCode.OK, before.StatusCode, await before.Content.ReadAsStringAsync());
        string grantId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var grants = new List<object>();
            await foreach (var grant in manager.FindAsync(subject: FinanceDatabase.Owner, client: null,
                               status: "valid", type: "permanent", scopes: null))
                grants.Add(grant);
            Assert.HasCount(1, grants);
            grantId = await manager.GetIdAsync(grants[0]) ?? throw new AssertFailedException("Grant has no identifier.");
        }
        using var page = await browser.GetAsync("/Account/Manage/ConnectedClients");
        var form = OAuthFlow.HiddenFields(await page.Content.ReadAsStringAsync());
        form["grantId"] = grantId;
        using var revoked = await browser.PostAsync("/Account/Manage/ConnectedClients/Revoke", new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.Found, revoked.StatusCode, await revoked.Content.ReadAsStringAsync());

        using var after = await McpHttpTests.SendAsync(bearer, "tools/list");

        Assert.AreEqual(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    /// <summary>The actual encrypted access token becomes unusable after its configured expiry.</summary>
    [TestMethod]
    public async Task Expired_access_token_is_rejected()
    {
        await using var factory = new MoneyBrainFactory(shortLivedTokens: true);
        await OAuthFlow.SeedAsync(factory);
        using var client = factory.CreateHttpsClient();
        var token = await OAuthFlow.IssueTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await Task.Delay(TimeSpan.FromSeconds(2));

        using var response = await McpHttpTests.SendAsync(client, "tools/list");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Instance write enablement cannot expand the scopes registered for a read-only client.</summary>
    [TestMethod]
    public async Task Read_only_client_cannot_request_write_scope_on_write_enabled_instance()
    {
        await using var factory = new MoneyBrainFactory(enableWrites: true);
        await OAuthFlow.SeedAsync(factory, allowWrites: false);
        using var client = factory.CreateHttpsClient();
        var parameters = OAuthFlow.AuthorizationParameters("moneybrain.read moneybrain.write");

        using var response = await client.GetAsync(QueryHelpers.AddQueryString("/connect/authorize", parameters));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "error:invalid_request");
    }

    /// <summary>Another browser user cannot revoke a grant by guessing its identifier.</summary>
    [TestMethod]
    public async Task Revocation_is_scoped_to_the_grant_owner()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var owner = factory.CreateHttpsClient();
        var token = await OAuthFlow.IssueTokenAsync(owner);
        string grantId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await users.CreateAsync(new ApplicationUser
            {
                Id = FinanceDatabase.Other, UserName = "other@example.test", Email = "other@example.test"
            }, OAuthFlow.Password);
            Assert.IsTrue(created.Succeeded);
            var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var grants = new List<object>();
            await foreach (var grant in authorizations.FindAsync(subject: FinanceDatabase.Owner, client: null,
                               status: "valid", type: "permanent", scopes: null))
                grants.Add(grant);
            Assert.HasCount(1, grants);
            grantId = await authorizations.GetIdAsync(grants[0]) ?? throw new AssertFailedException("Grant has no identifier.");
        }
        using var other = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(other, "other@example.test");
        using var page = await other.GetAsync("/Account/Manage/ConnectedClients");
        Assert.IsFalse((await page.Content.ReadAsStringAsync()).Contains(grantId, StringComparison.Ordinal));
        using var consentPage = await other.GetAsync(QueryHelpers.AddQueryString("/connect/authorize",
            OAuthFlow.AuthorizationParameters()));
        var form = OAuthFlow.HiddenFields(await consentPage.Content.ReadAsStringAsync());
        form["grantId"] = grantId;

        using var response = await other.PostAsync("/Account/Manage/ConnectedClients/Revoke", new FormUrlEncodedContent(form));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var stillValid = await McpHttpTests.SendAsync(owner, "tools/list");
        Assert.AreEqual(HttpStatusCode.OK, stillValid.StatusCode, await stillValid.Content.ReadAsStringAsync());
    }

    /// <summary>Repeated explicit consent reuses the same exact-scope grant instead of accumulating duplicates.</summary>
    [TestMethod]
    public async Task Repeated_accepted_consent_reuses_exact_scope_grant_but_not_different_scopes()
    {
        await using var factory = new MoneyBrainFactory(enableWrites: true);
        await OAuthFlow.SeedAsync(factory, allowWrites: true);
        using var client = factory.CreateHttpsClient();
        await OAuthFlow.LoginAsync(client);
        await OAuthFlow.AuthorizeAsync(client);
        await OAuthFlow.AuthorizeAsync(client);
        await OAuthFlow.AuthorizeAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}");
        await OAuthFlow.AuthorizeAsync(client, $"{McpAccess.ReadScope} {McpAccess.WriteScope}");

        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var grants = new List<object>();
        await foreach (var grant in manager.FindAsync(subject: FinanceDatabase.Owner, client: null,
                           status: "valid", type: "permanent", scopes: null))
            grants.Add(grant);
        Assert.HasCount(2, grants);
        var scopes = new List<string[]>();
        foreach (var grant in grants)
            scopes.Add((await manager.GetScopesAsync(grant)).ToArray());
        Assert.AreEqual(1, scopes.Count(values => values.ToHashSet().SetEquals([McpAccess.ReadScope])));
        Assert.AreEqual(1, scopes.Count(values => values.ToHashSet().SetEquals([McpAccess.ReadScope, McpAccess.WriteScope])));
    }

    /// <summary>Revoking all traverses more than one page and excludes other users and non-MCP grants.</summary>
    [TestMethod]
    public async Task Revoke_all_handles_more_than_one_hundred_grants_without_touching_unrelated_grants()
    {
        await using var factory = new MoneyBrainFactory();
        await OAuthFlow.SeedAsync(factory);
        using var browser = factory.CreateHttpsClient();
        var bearer = await OAuthFlow.IssueTokenAsync(browser);
        string unrelatedGrantId;
        string foreignGrantId;
        using (var scope = factory.Services.CreateScope())
        {
            var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = await applications.FindByClientIdAsync(OAuthFlow.ClientId);
            Assert.IsNotNull(application);
            var applicationId = await applications.GetIdAsync(application) ??
                                throw new AssertFailedException("Application has no identifier.");
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.IsTrue((await users.CreateAsync(new ApplicationUser
            {
                Id = FinanceDatabase.Other, UserName = "other@example.test", Email = "other@example.test"
            }, OAuthFlow.Password)).Succeeded);
            var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity("Grant test fixture"));
            for (var index = 0; index < 101; index++)
                await authorizations.CreateAsync(principal, FinanceDatabase.Owner, applicationId,
                    "permanent", ImmutableArray.Create(McpAccess.ReadScope));
            var unrelated = await authorizations.CreateAsync(principal, FinanceDatabase.Owner, applicationId,
                "permanent", ImmutableArray.Create("unrelated.scope"));
            unrelatedGrantId = await authorizations.GetIdAsync(unrelated) ??
                               throw new AssertFailedException("Unrelated grant has no identifier.");
            var foreign = await authorizations.CreateAsync(principal, FinanceDatabase.Other, applicationId,
                "permanent", ImmutableArray.Create(McpAccess.ReadScope));
            foreignGrantId = await authorizations.GetIdAsync(foreign) ??
                             throw new AssertFailedException("Foreign grant has no identifier.");
        }
        using var page = await browser.GetAsync("/Account/Manage/ConnectedClients");
        var form = OAuthFlow.HiddenFields(await page.Content.ReadAsStringAsync());

        using var response = await browser.PostAsync("/Account/Manage/ConnectedClients/RevokeAll",
            new FormUrlEncodedContent(form));

        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode, await response.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var validMcpGrants = new List<object>();
            await foreach (var grant in authorizations.FindAsync(subject: FinanceDatabase.Owner, client: null,
                               status: "valid", type: "permanent", scopes: ImmutableArray.Create(McpAccess.ReadScope)))
                validMcpGrants.Add(grant);
            Assert.IsEmpty(validMcpGrants);
            var unrelated = await authorizations.FindByIdAsync(unrelatedGrantId);
            var foreign = await authorizations.FindByIdAsync(foreignGrantId);
            Assert.IsNotNull(unrelated);
            Assert.IsNotNull(foreign);
            Assert.AreEqual("valid", await authorizations.GetStatusAsync(unrelated));
            Assert.AreEqual("valid", await authorizations.GetStatusAsync(foreign));
        }
        using var protocol = factory.CreateHttpsClient();
        protocol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var denied = await McpHttpTests.SendAsync(protocol, "tools/list");
        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
}

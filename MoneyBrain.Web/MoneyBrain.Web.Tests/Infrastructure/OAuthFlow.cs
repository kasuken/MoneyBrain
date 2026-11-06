using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Mcp;
using MoneyBrain.Web.Data;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MoneyBrain.Web.Tests.Infrastructure;

internal static class OAuthFlow
{
    internal const string ClientId = "integration-test-client";
    internal const string RedirectUri = "https://client.example.test/callback";
    internal const string Resource = "https://localhost/mcp";
    internal const string Password = "Only-for-tests_47!";
    internal static readonly string Verifier = new('a', 64);
    internal static readonly string Challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    internal static async Task SeedAsync(MoneyBrainFactory factory, bool allowWrites = false)
    {
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await users.CreateAsync(new ApplicationUser
        {
            Id = FinanceDatabase.Owner, UserName = "owner@example.test", Email = "owner@example.test",
            EmailConfirmed = true
        }, Password);
        Assert.IsTrue(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId, DisplayName = "Integration test client",
            ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Explicit
        };
        descriptor.RedirectUris.Add(new Uri(RedirectUri));
        descriptor.Permissions.UnionWith([
            Permissions.Endpoints.Authorization, Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + McpAccess.ReadScope
        ]);
        if (allowWrites)
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + McpAccess.WriteScope);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        descriptor.AddResourcePermissions(Resource);
        await applications.CreateAsync(descriptor);
    }

    internal static Dictionary<string, string?> AuthorizationParameters(string scope = McpAccess.ReadScope) => new()
    {
        ["client_id"] = ClientId, ["redirect_uri"] = RedirectUri, ["response_type"] = "code",
        ["scope"] = scope, ["state"] = "test-state", ["code_challenge"] = Challenge,
        ["code_challenge_method"] = "S256", ["resource"] = Resource
    };

    internal static async Task LoginAsync(HttpClient client, string email = "owner@example.test")
    {
        using var page = await client.GetAsync("/Account/Login");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        var form = HiddenFields(await page.Content.ReadAsStringAsync());
        form["_handler"] = "login";
        form["Input.Email"] = email;
        form["Input.Password"] = Password;
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    internal static async Task<string> AuthorizeAsync(HttpClient client, string scope = McpAccess.ReadScope)
    {
        var parameters = AuthorizationParameters(scope);
        var path = QueryHelpers.AddQueryString("/connect/authorize", parameters);
        using var page = await client.GetAsync(path);
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode, await page.Content.ReadAsStringAsync());
        var form = HiddenFields(await page.Content.ReadAsStringAsync());
        form["decision"] = "accept";
        using var consent = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.Found, consent.StatusCode, await consent.Content.ReadAsStringAsync());
        var location = consent.Headers.Location ?? throw new AssertFailedException("Consent did not redirect.");
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.AreEqual("test-state", query["state"].ToString());
        Assert.IsFalse(query.ContainsKey("error"), location.ToString());
        return query["code"].ToString();
    }

    internal static Task<HttpResponseMessage> ExchangeAsync(HttpClient client, string code,
        string? verifier = null, string? resource = Resource, string redirectUri = RedirectUri)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = ClientId,
            ["redirect_uri"] = redirectUri, ["code"] = code, ["code_verifier"] = verifier ?? Verifier
        };
        if (resource is not null)
            form["resource"] = resource;
        return client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    internal static async Task<string> IssueTokenAsync(HttpClient client, string scope = McpAccess.ReadScope,
        bool login = true)
    {
        if (login)
            await LoginAsync(client);
        var code = await AuthorizeAsync(client, scope);
        using var response = await ExchangeAsync(client, code);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
        using var payload = JsonDocument.Parse(body);
        Assert.IsFalse(payload.RootElement.TryGetProperty("refresh_token", out _));
        Assert.IsTrue(payload.RootElement.GetProperty("expires_in").GetInt32() >= 0);
        return payload.RootElement.GetProperty("access_token").GetString() ??
               throw new AssertFailedException("Token response omitted access_token.");
    }

    internal static Dictionary<string, string> HiddenFields(string html)
    {
        var fields = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var attributes = Regex.Matches(input.Value, "([\\w-]+)=\"([^\"]*)\"")
                .ToDictionary(match => match.Groups[1].Value, match => WebUtility.HtmlDecode(match.Groups[2].Value),
                    StringComparer.OrdinalIgnoreCase);
            if (attributes.GetValueOrDefault("type") == "hidden" &&
                attributes.TryGetValue("name", out var name))
                fields[name] = attributes.GetValueOrDefault("value", "");
        }
        Assert.IsTrue(fields.ContainsKey("__RequestVerificationToken"), "Form did not provide an antiforgery token.");
        return fields;
    }
}

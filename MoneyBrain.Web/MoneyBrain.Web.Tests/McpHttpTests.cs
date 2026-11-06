using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Checks the opt-in MCP HTTP surface and its real authentication challenge.</summary>
[TestClass]
public sealed class McpHttpTests
{
    /// <summary>Disabled installations expose neither protocol nor discovery endpoints.</summary>
    [TestMethod]
    [DataRow("/mcp")]
    [DataRow("/.well-known/oauth-protected-resource/mcp")]
    [DataRow("/.well-known/oauth-authorization-server")]
    [DataRow("/connect/authorize")]
    [DataRow("/connect/token")]
    public async Task Disabled_Mcp_routes_are_not_available(string path)
    {
        await using var factory = new MoneyBrainFactory(enabled: false);
        using var client = factory.CreateHttpsClient();

        using var response = path == "/mcp"
            ? await SendAsync(client, "initialize")
            : await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A disabled installation cannot issue OAuth tokens through POST either.</summary>
    [TestMethod]
    public async Task Disabled_OAuth_token_endpoint_does_not_issue_tokens()
    {
        await using var factory = new MoneyBrainFactory(enabled: false);
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code" }));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Anonymous protocol operations challenge with RFC 9728 metadata, not a login redirect.</summary>
    [TestMethod]
    [DataRow("initialize")]
    [DataRow("tools/list")]
    [DataRow("tools/call")]
    [DataRow("resources/read")]
    public async Task Anonymous_protocol_requests_receive_bearer_challenge(string method)
    {
        await using var factory = new MoneyBrainFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await SendAsync(client, method);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
        StringAssert.Contains(string.Join(" ", response.Headers.WwwAuthenticate),
            "resource_metadata=\"https://localhost/.well-known/oauth-protected-resource/mcp\"");
    }

    /// <summary>Discovery advertises the canonical resource and only enabled scopes.</summary>
    [TestMethod]
    public async Task Discovery_describes_read_only_resource_and_authorization_code_PKCE()
    {
        await using var factory = new MoneyBrainFactory();
        using var client = factory.CreateHttpsClient();
        using var resourceResponse = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");
        Assert.AreEqual(HttpStatusCode.OK, resourceResponse.StatusCode);
        using var resource = JsonDocument.Parse(await resourceResponse.Content.ReadAsStringAsync());
        Assert.AreEqual(OAuthFlow.Resource, resource.RootElement.GetProperty("resource").GetString());
        CollectionAssert.AreEquivalent(new[] { "moneybrain.read" },
            resource.RootElement.GetProperty("scopes_supported").EnumerateArray().Select(value => value.GetString()).ToArray());

        using var authorizationResponse = await client.GetAsync("/.well-known/oauth-authorization-server");
        Assert.AreEqual(HttpStatusCode.OK, authorizationResponse.StatusCode);
        using var authorization = JsonDocument.Parse(await authorizationResponse.Content.ReadAsStringAsync());
        Assert.AreEqual("https://localhost/", authorization.RootElement.GetProperty("issuer").GetString());
        CollectionAssert.AreEquivalent(new[] { "authorization_code" },
            authorization.RootElement.GetProperty("grant_types_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
        CollectionAssert.AreEquivalent(new[] { "S256" },
            authorization.RootElement.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    /// <summary>Foreign browser origins are rejected before reaching protocol dispatch.</summary>
    [TestMethod]
    public async Task Unregistered_origin_is_rejected()
    {
        await using var factory = new MoneyBrainFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example.test");

        using var response = await SendAsync(client, "initialize");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, object? parameters = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 1, method, @params = parameters ?? new { }
            }), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        return await client.SendAsync(request);
    }

    internal static async Task<JsonDocument> ReadRpcAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, text);
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            text = string.Join("", text.Split('\n').Where(line => line.StartsWith("data: "))
                .Select(line => line["data: ".Length..]));
        var document = JsonDocument.Parse(text);
        Assert.IsFalse(document.RootElement.TryGetProperty("error", out _), text);
        return document;
    }
}

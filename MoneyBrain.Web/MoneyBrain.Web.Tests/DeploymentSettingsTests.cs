using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoneyBrain.Web.Application.Common.Configuration;
using MoneyBrain.Web.Tests.Infrastructure;

namespace MoneyBrain.Web.Tests;

/// <summary>Reverse-proxy handling, HTTPS redirection and the self-hosted marketing switch.</summary>
[TestClass]
public sealed class DeploymentSettingsTests
{
    private static readonly IReadOnlyDictionary<string, string?> Proxied = new Dictionary<string, string?>
    {
        ["ForwardedHeaders:Enabled"] = "true"
    };

    private static HttpClient PlainClient(MoneyBrainFactory factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    private static HttpRequestMessage Get(string path, string? proto = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (proto is not null)
            request.Headers.Add("X-Forwarded-Proto", proto);
        return request;
    }

    /// <summary>The proxy says the client used plain HTTP, so the app redirects to HTTPS on the public port.</summary>
    [TestMethod]
    public async Task Behind_a_proxy_http_requests_are_redirected_to_https()
    {
        await using var factory = new MoneyBrainFactory(enabled: false, extraSettings: Proxied);
        using var client = PlainClient(factory);

        using var response = await client.SendAsync(Get("/Account/Login", "http"));

        Assert.IsTrue(response.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect);
        Assert.AreEqual("https://localhost/Account/Login", response.Headers.Location?.ToString());
    }

    /// <summary>TLS is terminated by the proxy; an HTTPS client must not be redirected in a loop.</summary>
    [TestMethod]
    public async Task Behind_a_proxy_https_requests_are_not_redirected()
    {
        await using var factory = new MoneyBrainFactory(enabled: false, extraSettings: Proxied);
        using var client = PlainClient(factory);

        using var response = await client.SendAsync(Get("/Account/Login", "https"));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Container health checks speak plain HTTP and must not be redirected.</summary>
    [TestMethod]
    [DataRow("/health/live")]
    [DataRow("/health/ready")]
    public async Task Health_probes_are_never_redirected(string path)
    {
        await using var factory = new MoneyBrainFactory(enabled: false, extraSettings: Proxied);
        using var client = PlainClient(factory);

        using var response = await client.SendAsync(Get(path, "http"));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Without a proxy the container only speaks HTTP, so redirecting would lead nowhere.</summary>
    [TestMethod]
    public void Https_redirection_defaults_follow_the_deployment_shape()
    {
        Assert.IsFalse(DeploymentSettings.HttpsRedirectionEnabled(Config(), isDevelopment: false));
        Assert.IsTrue(DeploymentSettings.HttpsRedirectionEnabled(Config(("ForwardedHeaders:Enabled", "true")), isDevelopment: false));
        Assert.IsTrue(DeploymentSettings.HttpsRedirectionEnabled(Config(("HttpsRedirection:Enabled", "true")), isDevelopment: false));
        Assert.IsFalse(DeploymentSettings.HttpsRedirectionEnabled(
            Config(("ForwardedHeaders:Enabled", "true"), ("HttpsRedirection:Enabled", "false")), isDevelopment: false));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    /// <summary>Self-hosted defaults (licensing off): the marketing site is not served.</summary>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/features")]
    [DataRow("/pricing")]
    [DataRow("/about")]
    public async Task Self_hosted_defaults_do_not_serve_marketing_pages(string path)
    {
        await using var factory = new MoneyBrainFactory(enabled: false);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/Account/Login", response.Headers.Location?.OriginalString);
    }

    /// <summary>The hosted service (licensing on) keeps its public landing and pricing pages.</summary>
    [TestMethod]
    [DataRow("/")]
    [DataRow("/features")]
    [DataRow("/pricing")]
    [DataRow("/about")]
    public async Task Hosted_licensing_keeps_marketing_pages(string path)
    {
        await using var factory = new MoneyBrainFactory(enabled: false,
            extraSettings: new Dictionary<string, string?> { ["Licensing:Enabled"] = "true" });
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The setting can force the marketing site on for a self-hosted instance.</summary>
    [TestMethod]
    public async Task Marketing_setting_overrides_the_default()
    {
        await using var factory = new MoneyBrainFactory(enabled: false,
            extraSettings: new Dictionary<string, string?> { ["Marketing:Enabled"] = "true" });
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/about");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Self-hosted instances without Stripe do not expose the webhook endpoint.</summary>
    [TestMethod]
    public async Task Stripe_webhook_is_only_mapped_when_licensing_is_enabled()
    {
        await using var off = new MoneyBrainFactory(enabled: false);
        Assert.IsFalse(HasWebhookEndpoint(off));

        await using var on = new MoneyBrainFactory(enabled: false,
            extraSettings: new Dictionary<string, string?> { ["Licensing:Enabled"] = "true" });
        Assert.IsTrue(HasWebhookEndpoint(on));
        await Task.CompletedTask;
    }

    private static bool HasWebhookEndpoint(MoneyBrainFactory factory)
    {
        _ = factory.Server; // build the host
        return factory.Services.GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Any(endpoint => endpoint.RoutePattern.RawText == "/api/stripe/webhook");
    }
}

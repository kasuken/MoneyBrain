using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace MoneyBrain.Web.Application.Common.Configuration;

/// <summary>
/// Deployment-shape switches that decide how the app behaves behind a reverse proxy and
/// whether it serves the public marketing site. Kept pure and static so the rules are unit-testable.
/// </summary>
public static class DeploymentSettings
{
    /// <summary>Public paths served by the marketing site.</summary>
    private static readonly string[] MarketingPaths = ["/", "/features", "/pricing", "/about"];

    /// <summary>
    /// True when the app sits behind a TLS-terminating reverse proxy that sends X-Forwarded-* headers.
    /// <c>ForwardedHeaders:Enabled</c> wins; otherwise the ASP.NET Core variable
    /// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> (set by Azure App Service) is honoured.
    /// </summary>
    public static bool ForwardedHeadersEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool?>("ForwardedHeaders:Enabled")
        ?? configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED");

    /// <summary>
    /// True when plain-HTTP requests should be redirected to HTTPS. Behind a proxy the scheme comes from
    /// <c>X-Forwarded-Proto</c>, so redirecting is safe and correct there. Without a proxy the container only
    /// speaks HTTP, so a redirect would lead nowhere unless <c>HttpsRedirection:Enabled</c> is set explicitly.
    /// </summary>
    public static bool HttpsRedirectionEnabled(IConfiguration configuration, bool isDevelopment) =>
        configuration.GetValue<bool?>("HttpsRedirection:Enabled")
        ?? (ForwardedHeadersEnabled(configuration) || isDevelopment);

    /// <summary>
    /// True when the public marketing/sign-up pages are served. Defaults to <c>Licensing:Enabled</c>: the hosted
    /// service (licensing on) shows them, self-hosted instances (licensing off) do not.
    /// </summary>
    public static bool MarketingEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool?>("Marketing:Enabled")
        ?? configuration.GetValue<bool>("Licensing:Enabled");

    /// <summary>
    /// Whether a request for <paramref name="path"/> must not reach the marketing pages, given the settings.
    /// <c>/pricing</c> stays reachable while licensing is enabled because checkout and licence screens link to it.
    /// </summary>
    public static bool IsBlockedMarketingPath(string? path, bool marketingEnabled, bool licensingEnabled)
    {
        if (marketingEnabled || string.IsNullOrEmpty(path))
            return false;

        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        if (licensingEnabled && normalized.Equals("/pricing", StringComparison.OrdinalIgnoreCase))
            return false;

        return MarketingPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies <c>ForwardedHeaders:KnownProxies</c> / <c>ForwardedHeaders:KnownNetworks</c>. With neither configured the
    /// default loopback-only trust is cleared, so a proxy container on a Docker network is honoured.
    /// Do not publish the app port directly in that case.
    /// </summary>
    public static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

        foreach (var proxy in proxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        foreach (var network in networks)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
}

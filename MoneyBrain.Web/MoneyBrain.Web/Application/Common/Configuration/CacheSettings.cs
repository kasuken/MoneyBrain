namespace MoneyBrain.Web.Application.Common.Configuration;

/// <summary>
/// Cache configuration settings.
/// Controls which cache provider to use and provider-specific settings.
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// Cache provider to use. Valid values: "Memory", "Redis".
    /// Defaults to "Memory" for in-memory caching.
    /// </summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>
    /// Redis cache provider settings.
    /// Only used when Provider is set to "Redis".
    /// </summary>
    public RedisCacheSettings? Redis { get; set; }
}

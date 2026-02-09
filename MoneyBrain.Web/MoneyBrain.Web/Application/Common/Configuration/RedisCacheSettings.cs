namespace MoneyBrain.Web.Application.Common.Configuration;

/// <summary>
/// Redis cache provider configuration settings.
/// </summary>
public class RedisCacheSettings
{
    /// <summary>
    /// Redis connection string.
    /// Format: hostname:port or hostname:port,password=xxx
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Redis database number to use (0-15 for default Redis configuration).
    /// Defaults to 0.
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Instance name prefix for cache keys.
    /// Useful for separating cache namespaces in shared Redis instances.
    /// Optional.
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Enable SSL/TLS for Redis connection.
    /// Defaults to false.
    /// </summary>
    public bool SslEnabled { get; set; } = false;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// Defaults to 5000ms (5 seconds).
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Synchronous operation timeout in milliseconds.
    /// Defaults to 5000ms (5 seconds).
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;
}

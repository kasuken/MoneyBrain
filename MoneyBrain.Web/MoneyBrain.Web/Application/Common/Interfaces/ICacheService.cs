namespace MoneyBrain.Web.Application.Common.Interfaces;

/// <summary>
/// Cache service for storing and retrieving data with optional expiration.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or null if not found or expired.</returns>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Stores a value in the cache with optional expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiration">Optional expiration time. Defaults to 1 hour if not specified.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes all cached values matching the specified pattern.
    /// Supports wildcard patterns like "user:123:*".
    /// </summary>
    /// <param name="pattern">The pattern to match cache keys.</param>
    Task RemoveByPatternAsync(string pattern);
}

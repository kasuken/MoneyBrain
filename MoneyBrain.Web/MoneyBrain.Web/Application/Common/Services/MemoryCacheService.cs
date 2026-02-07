using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using MoneyBrain.Web.Application.Common.Interfaces;

namespace MoneyBrain.Web.Application.Common.Services;

/// <summary>
/// In-memory cache service implementation using IMemoryCache.
/// Thread-safe with pattern-based invalidation support.
/// </summary>
public class MemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = _memoryCache.Get<T>(key);
        return Task.FromResult(value);
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };

        options.RegisterPostEvictionCallback((k, v, reason, state) =>
        {
            // Remove key from tracking when evicted
            _keys.TryRemove(k.ToString()!, out _);
        });

        _memoryCache.Set(key, value, options);
        _keys.TryAdd(key, 0);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveByPatternAsync(string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.Compiled);

        var matchingKeys = _keys.Keys.Where(k => regex.IsMatch(k)).ToList();

        foreach (var key in matchingKeys)
        {
            _memoryCache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

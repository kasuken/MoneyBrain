using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MoneyBrain.Web.Application.Common.Interfaces;
using StackExchange.Redis;

namespace MoneyBrain.Web.Application.Common.Services;

/// <summary>
/// Redis-based cache service implementation using StackExchange.Redis.
/// Thread-safe with pattern-based invalidation support using SCAN.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _instancePrefix;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="instanceName">Optional instance name prefix for cache keys.</param>
    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger, string? instanceName = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = _redis.GetDatabase();
        _instancePrefix = string.IsNullOrWhiteSpace(instanceName) ? string.Empty : $"{instanceName}:";
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var value = await _database.StringGetAsync(redisKey);

            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache key: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var redisKey = GetRedisKey(key);
            var serialized = JsonSerializer.Serialize(value);
            var expirationTime = expiration ?? DefaultExpiration;

            await _database.StringSetAsync(redisKey, serialized, expirationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key)
    {
        try
        {
            var redisKey = GetRedisKey(key);
            await _database.KeyDeleteAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var redisPattern = GetRedisKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            // Use SCAN instead of KEYS for production safety
            var keys = server.KeysAsync(pattern: redisPattern, pageSize: 250);
            var deleteTasks = new List<Task>();

            await foreach (var key in keys)
            {
                deleteTasks.Add(_database.KeyDeleteAsync(key));
                
                // Batch deletions to avoid overwhelming Redis
                if (deleteTasks.Count >= 100)
                {
                    await Task.WhenAll(deleteTasks);
                    deleteTasks.Clear();
                }
            }

            // Delete remaining keys
            if (deleteTasks.Count > 0)
            {
                await Task.WhenAll(deleteTasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache keys by pattern: {Pattern}", pattern);
        }
    }

    private string GetRedisKey(string key)
    {
        return _instancePrefix + key;
    }
}

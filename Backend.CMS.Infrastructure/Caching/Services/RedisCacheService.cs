using Backend.CMS.Infrastructure.Caching.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    /// <summary>
    /// Redis-based cache service implementation
    /// </summary>
    public class RedisCacheService : ICacheService, ICacheInvalidationService, ICacheMonitoringService, IDisposable
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _redis;
        private readonly ICacheKeyService _keyService;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly CacheOptions _options;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lockSemaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
        private readonly Timer _cleanupTimer;
        private readonly CacheStatistics _statistics;
        private bool _disposed = false;

        public RedisCacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer redis,
            ICacheKeyService keyService,
            ILogger<RedisCacheService> logger,
            IOptions<CacheOptions> options)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _keyService = keyService ?? throw new ArgumentNullException(nameof(keyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));

            _lockSemaphore = new SemaphoreSlim(1, 1);
            _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _statistics = new CacheStatistics();

            _jsonOptions = _options.JsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
            _jsonOptions = _options.JsonOptions ?? CacheOptions.GetDefaultJsonOptions();
            // Cleanup unused locks every 5 minutes
            _cleanupTimer = new Timer(CleanupUnusedLocks, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInformation("RedisCacheService initialized with prefix: {Prefix}", _options.KeyPrefix);
        }

        #region ICacheService Implementation

        public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            ArgumentNullException.ThrowIfNull(factory);

            var validatedKey = ValidateAndNormalizeKey(key);

            try
            {
                // Try to get from cache first
                var cachedItem = await GetAsync<T>(validatedKey, cancellationToken);
                if (cachedItem != null)
                {
                    RecordHit();
                    return cachedItem;
                }

                RecordMiss();

                // Use distributed locking to prevent cache stampede
                if (_options.EnableDistributedLocking)
                {
                    return await GetOrAddWithLockAsync(validatedKey, factory, expiration, cancellationToken);
                }

                // Fallback: simple local locking
                return await GetOrAddWithLocalLockAsync(validatedKey, factory, expiration, cancellationToken);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error in GetOrAddAsync for key: {Key}", validatedKey);

                // On cache failure, call factory directly
                try
                {
                    return await factory();
                }
                catch (Exception factoryEx)
                {
                    _logger.LogError(factoryEx, "Factory method also failed for key: {Key}", validatedKey);
                    throw;
                }
            }
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var validatedKey = ValidateAndNormalizeKey(key);

            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(validatedKey, cancellationToken);
                if (string.IsNullOrEmpty(cachedValue))
                    return null;

                var result = JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
                _logger.LogDebug("Cache hit for key: {Key}", validatedKey);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached item with key: {Key}. Removing corrupted entry.", validatedKey);
                await RemoveAsync(validatedKey, cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached item with key: {Key}", validatedKey);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            ArgumentNullException.ThrowIfNull(value);

            var validatedKey = ValidateAndNormalizeKey(key);
            var actualExpiration = expiration ?? _options.DefaultExpiration;

            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = actualExpiration
                };

                await _distributedCache.SetStringAsync(validatedKey, serializedValue, options, cancellationToken);
                _logger.LogDebug("Cache item set with key: {Key}, expiration: {Expiration}", validatedKey, actualExpiration);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error setting cached item with key: {Key}", validatedKey);
                throw;
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var validatedKey = ValidateAndNormalizeKey(key);

            try
            {
                await _distributedCache.RemoveAsync(validatedKey, cancellationToken);
                _logger.LogDebug("Cache item removed with key: {Key}", validatedKey);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error removing cached item with key: {Key}", validatedKey);
            }
        }

        public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            if (keys?.Any() != true)
                return;

            var validatedKeys = keys.Select(ValidateAndNormalizeKey).ToList();

            try
            {
                var database = _redis.GetDatabase();
                var redisKeys = validatedKeys.Select(k => new RedisKey(k)).ToArray();

                if (redisKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(redisKeys);
                    _logger.LogDebug("Removed {Count} cache items", redisKeys.Length);
                }
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error removing multiple cached items");

                // Fallback to individual removal
                var tasks = validatedKeys.Select(key => RemoveAsync(key, cancellationToken));
                await Task.WhenAll(tasks);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return;

            try
            {
                var server = GetRedisServer();
                var database = _redis.GetDatabase();

                var fullPattern = _options.KeyPrefix + _options.KeySeparator + pattern;
                const int batchSize = 1000;
                var deletedCount = 0;

                await foreach (var keyBatch in GetKeysBatchedAsync(server, fullPattern, batchSize, cancellationToken))
                {
                    if (keyBatch.Any())
                    {
                        await database.KeyDeleteAsync(keyBatch.ToArray());
                        deletedCount += keyBatch.Count();
                    }
                }

                _logger.LogInformation("Removed {Count} cache items matching pattern: {Pattern}", deletedCount, pattern);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error removing cached items by pattern: {Pattern}", pattern);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var validatedKey = ValidateAndNormalizeKey(key);

            try
            {
                var database = _redis.GetDatabase();
                return await database.KeyExistsAsync(validatedKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if cached item exists with key: {Key}", validatedKey);
                return false;
            }
        }

        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var validatedKey = ValidateAndNormalizeKey(key);

            try
            {
                await _distributedCache.RefreshAsync(validatedKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cached item with key: {Key}", validatedKey);
            }
        }

        public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default) where T : class
        {
            var result = new Dictionary<string, T?>();

            if (keys?.Any() != true)
                return result;

            var validatedKeys = keys.Select(ValidateAndNormalizeKey).ToList();

            try
            {
                var database = _redis.GetDatabase();
                var redisKeys = validatedKeys.Select(k => new RedisKey(k)).ToArray();

                var values = await database.StringGetAsync(redisKeys);

                for (int i = 0; i < validatedKeys.Count; i++)
                {
                    var key = validatedKeys[i];
                    var value = values[i];

                    if (value.HasValue)
                    {
                        try
                        {
                            var deserializedValue = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
                            result[key] = deserializedValue;
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize cached item with key: {Key}", key);
                            result[key] = null;
                        }
                    }
                    else
                    {
                        result[key] = null;
                    }
                }

                _logger.LogDebug("Retrieved {HitCount}/{TotalCount} items from cache",
                    result.Values.Count(v => v != null), validatedKeys.Count);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error getting multiple cached items");

                // Fallback to individual gets
                var tasks = validatedKeys.Select(async key => new { Key = key, Value = await GetAsync<T>(key, cancellationToken) });
                var results = await Task.WhenAll(tasks);

                foreach (var item in results)
                {
                    result[item.Key] = item.Value;
                }
            }

            return result;
        }

        public async Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            if (items?.Any() != true)
                return;

            var actualExpiration = expiration ?? _options.DefaultExpiration;

            try
            {
                var tasks = items.Select(kvp => SetAsync(kvp.Key, kvp.Value, actualExpiration, cancellationToken));
                await Task.WhenAll(tasks);

                _logger.LogDebug("Set {Count} cache items with expiration: {Expiration}", items.Count, actualExpiration);
            }
            catch (Exception ex)
            {
                RecordError();
                _logger.LogError(ex, "Error setting multiple cached items");
                throw;
            }
        }

        #endregion

        #region ICacheInvalidationService Implementation

        public async Task InvalidateEntityAsync<T>(object entityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var entityKey = _keyService.GetEntityKey<T>(entityId);
                await RemoveAsync(entityKey, cancellationToken);

                // Also remove related patterns
                var pattern = _keyService.GetEntityPattern<T>();
                await RemoveByPatternAsync($"{pattern}*{entityId}*", cancellationToken);

                _logger.LogDebug("Invalidated cache for entity {EntityType} with ID {EntityId}", typeof(T).Name, entityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for entity {EntityType} with ID {EntityId}", typeof(T).Name, entityId);
                throw;
            }
        }

        public async Task InvalidateEntitiesAsync<T>(IEnumerable<object> entityIds, CancellationToken cancellationToken = default)
        {
            if (entityIds?.Any() != true)
                return;

            try
            {
                var keys = entityIds.Select(id => _keyService.GetEntityKey<T>(id)).ToList();
                await RemoveAsync(keys, cancellationToken);

                _logger.LogDebug("Invalidated cache for {Count} entities of type {EntityType}", keys.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for multiple {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        public async Task InvalidateEntityTypeAsync<T>(CancellationToken cancellationToken = default)
        {
            try
            {
                var pattern = _keyService.GetEntityPattern<T>();
                await RemoveByPatternAsync(pattern, cancellationToken);

                _logger.LogDebug("Invalidated all cache entries for entity type {EntityType}", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for entity type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public async Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            await RemoveByPatternAsync(pattern, cancellationToken);
        }

        public async Task InvalidateRelatedAsync<T>(object entityId, CancellationToken cancellationToken = default)
        {
            try
            {
                // This method can be overridden in derived classes for specific entity relationships
                var entityType = typeof(T).Name.ToLowerInvariant();

                // Common patterns for related data
                var patterns = new[]
                {
                    $"{entityType}*{entityId}*",
                    $"*{entityType}_{entityId}*",
                    $"list*{entityType}*",
                    $"search*{entityType}*"
                };

                var tasks = patterns.Select(pattern => RemoveByPatternAsync(pattern, cancellationToken));
                await Task.WhenAll(tasks);

                _logger.LogDebug("Invalidated related cache entries for entity {EntityType} with ID {EntityId}", typeof(T).Name, entityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating related cache for entity {EntityType} with ID {EntityId}", typeof(T).Name, entityId);
                throw;
            }
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var server = GetRedisServer();
                await server.FlushDatabaseAsync();

                _logger.LogWarning("Cleared all cache entries from database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache entries");
                throw;
            }
        }

        #endregion

        #region ICacheMonitoringService Implementation

        public Task<CacheStatistics> GetStatisticsAsync()
        {
            return Task.FromResult(_statistics);
        }

        public Task ResetStatisticsAsync()
        {
            _statistics.Reset();
            _logger.LogInformation("Cache statistics reset");
            return Task.CompletedTask;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var database = _redis.GetDatabase();
                var result = await database.PingAsync();
                return result.TotalMilliseconds < 1000; // Consider healthy if ping < 1 second
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetCacheInfoAsync()
        {
            try
            {
                var server = GetRedisServer();
                var info = await server.InfoAsync();

                var result = new Dictionary<string, object>();

                foreach (var section in info)
                {
                    foreach (var item in section)
                    {
                        result[$"{section.Key}_{item.Key}"] = item.Value;
                    }
                }

                // Add our own statistics
                result["custom_hit_count"] = _statistics.HitCount;
                result["custom_miss_count"] = _statistics.MissCount;
                result["custom_error_count"] = _statistics.ErrorCount;
                result["custom_hit_ratio"] = _statistics.HitRatio;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache info");
                return new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<T?> GetOrAddWithLockAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration, CancellationToken cancellationToken) where T : class
        {
            var lockKey = $"lock:{key}";
            var database = _redis.GetDatabase();
            var lockValue = Guid.NewGuid().ToString();

            try
            {
                // Try to acquire distributed lock
                var acquired = await database.StringSetAsync(lockKey, lockValue, _options.LockTimeout, When.NotExists);

                if (acquired)
                {
                    try
                    {
                        // Double-check pattern - item might have been added while waiting for lock
                        var item = await GetAsync<T>(key, cancellationToken);
                        if (item != null)
                            return item;

                        // Generate and cache the item
                        item = await factory();
                        if (item != null)
                        {
                            await SetAsync(key, item, expiration, cancellationToken);
                        }

                        return item;
                    }
                    finally
                    {
                        // Release lock
                        const string script = @"
                            if redis.call('get', KEYS[1]) == ARGV[1] then
                                return redis.call('del', KEYS[1])
                            else
                                return 0
                            end";

                        await database.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
                    }
                }
                else
                {
                    // Wait a bit and try to get from cache (might have been populated by lock holder)
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                    var item = await GetAsync<T>(key, cancellationToken);

                    // If still not available, call factory without lock (acceptable risk)
                    return item ?? await factory();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in distributed locking for key: {Key}", key);
                // Fallback to factory call
                return await factory();
            }
        }

        private async Task<T?> GetOrAddWithLocalLockAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration, CancellationToken cancellationToken) where T : class
        {
            var semaphore = GetKeyLock(key);

            try
            {
                await semaphore.WaitAsync(cancellationToken);

                // Double-check pattern
                var item = await GetAsync<T>(key, cancellationToken);
                if (item != null)
                    return item;

                // Generate and cache the item
                item = await factory();
                if (item != null)
                {
                    await SetAsync(key, item, expiration, cancellationToken);
                }

                return item;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private SemaphoreSlim GetKeyLock(string key)
        {
            return _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        private void CleanupUnusedLocks(object? state)
        {
            if (_keyLocks.Count <= 1000) // Only cleanup if we have many locks
                return;

            var keysToRemove = new List<string>();

            foreach (var kvp in _keyLocks)
            {
                var semaphore = kvp.Value;
                if (semaphore.CurrentCount == 1) // No one is waiting
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove.Take(500)) // Limit cleanup
            {
                if (_keyLocks.TryRemove(key, out var semaphore))
                {
                    semaphore.Dispose();
                }
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} unused key locks", Math.Min(keysToRemove.Count, 500));
            }
        }

        private string ValidateAndNormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var normalizedKey = $"{_options.KeyPrefix}{_options.KeySeparator}{key}";

            if (normalizedKey.Length > _options.MaxKeyLength)
            {
                // Use hash for very long keys
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedKey));
                var hash = Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=');
                normalizedKey = $"{_options.KeyPrefix}{_options.KeySeparator}hash:{hash}";
            }

            return normalizedKey;
        }

        private IServer GetRedisServer()
        {
            var endpoints = _redis.GetEndPoints();
            if (!endpoints.Any())
                throw new InvalidOperationException("No Redis endpoints available");

            return _redis.GetServer(endpoints.First());
        }

        private async IAsyncEnumerable<IEnumerable<RedisKey>> GetKeysBatchedAsync(IServer server, string pattern, int batchSize, CancellationToken cancellationToken)
        {
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                keys.Add(key);

                if (keys.Count >= batchSize)
                {
                    yield return keys.ToArray();
                    keys.Clear();
                }
            }

            if (keys.Any())
            {
                yield return keys.ToArray();
            }
        }

        private void RecordHit()
        {
            _statistics.IncrementHits();
            _statistics.IncrementOperation("hit");
        }

        private void RecordMiss()
        {
            _statistics.IncrementMisses();
            _statistics.IncrementOperation("miss");
        }

        private void RecordError()
        {
            _statistics.IncrementErrors();
            _statistics.IncrementOperation("error");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cleanupTimer?.Dispose();
                _lockSemaphore?.Dispose();

                foreach (var semaphore in _keyLocks.Values)
                {
                    semaphore.Dispose();
                }
                _keyLocks.Clear();

                _disposed = true;
            }
        }

        #endregion
    }
}
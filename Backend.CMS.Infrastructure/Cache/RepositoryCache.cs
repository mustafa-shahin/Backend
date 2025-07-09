using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Cache
{
    /// <summary>
    /// Repository-level caching implementation using IMemoryCache
    /// </summary>
    public class RepositoryCache : IRepositoryCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RepositoryCache> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _keyTracker;
        private readonly ConcurrentDictionary<string, long> _statistics;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string HIT_COUNT_KEY = "cache:hit_count";
        private const string MISS_COUNT_KEY = "cache:miss_count";
        private const string LAST_CLEAR_KEY = "cache:last_clear";

        public RepositoryCache(IMemoryCache memoryCache, ILogger<RepositoryCache> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyTracker = new ConcurrentDictionary<string, DateTime>();
            _statistics = new ConcurrentDictionary<string, long>();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Initialize statistics
            _statistics.TryAdd(HIT_COUNT_KEY, 0);
            _statistics.TryAdd(MISS_COUNT_KEY, 0);
            _statistics.TryAdd(LAST_CLEAR_KEY, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
                }

                var cacheKey = GenerateCacheKey<T>(key);

                if (_memoryCache.TryGetValue(cacheKey, out var cachedValue))
                {
                    _statistics.AddOrUpdate(HIT_COUNT_KEY, 1, (k, v) => v + 1);
                    _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);

                    return cachedValue as T;
                }

                _statistics.AddOrUpdate(MISS_COUNT_KEY, 1, (k, v) => v + 1);
                _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
                }

                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var cacheKey = GenerateCacheKey<T>(key);
                var cacheEntryOptions = new MemoryCacheEntryOptions();

                if (expiration.HasValue)
                {
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = expiration.Value;
                }
                else
                {
                    // Default expiration of 30 minutes
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                }

                // Set sliding expiration to 5 minutes
                cacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(5);

                // Set priority
                cacheEntryOptions.Priority = CacheItemPriority.Normal;

                // Add eviction callback
                cacheEntryOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);

                _memoryCache.Set(cacheKey, value, cacheEntryOptions);
                _keyTracker.TryAdd(cacheKey, DateTime.UtcNow);

                _logger.LogDebug("Cached value for key: {CacheKey} with expiration: {Expiration}", cacheKey, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
                }

                var cacheKey = key.Contains(":") ? key : $"repo:{key}";
                _memoryCache.Remove(cacheKey);
                _keyTracker.TryRemove(cacheKey, out _);

                _logger.LogDebug("Removed cached value for key: {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing value from cache for key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(pattern))
                {
                    throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
                }

                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var keysToRemove = _keyTracker.Keys.Where(key => regex.IsMatch(key)).ToList();

                foreach (var key in keysToRemove)
                {
                    _memoryCache.Remove(key);
                    _keyTracker.TryRemove(key, out _);
                }

                _logger.LogDebug("Removed {Count} cached values matching pattern: {Pattern}", keysToRemove.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing values from cache by pattern: {Pattern}", pattern);
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key))
                {
                    throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
                }

                if (factory == null)
                {
                    throw new ArgumentNullException(nameof(factory));
                }

                var cachedValue = await GetAsync<T>(key, cancellationToken);
                if (cachedValue != null)
                {
                    return cachedValue;
                }

                var value = await factory();
                if (value != null)
                {
                    await SetAsync(key, value, expiration, cancellationToken);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                var cacheKey = key.Contains(":") ? key : $"repo:{key}";
                return _keyTracker.ContainsKey(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists in cache: {Key}", key);
                return false;
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var keysToRemove = _keyTracker.Keys.ToList();
                foreach (var key in keysToRemove)
                {
                    _memoryCache.Remove(key);
                }

                _keyTracker.Clear();
                _statistics[LAST_CLEAR_KEY] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                _logger.LogInformation("Cleared {Count} items from cache", keysToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            try
            {
                var hitCount = (int)_statistics.GetValueOrDefault(HIT_COUNT_KEY, 0);
                var missCount = (int)_statistics.GetValueOrDefault(MISS_COUNT_KEY, 0);
                var lastClearTime = DateTimeOffset.FromUnixTimeSeconds(_statistics.GetValueOrDefault(LAST_CLEAR_KEY, 0)).DateTime;

                return new CacheStatistics
                {
                    KeyCount = _keyTracker.Count,
                    TotalMemorySize = EstimateMemorySize(),
                    HitCount = hitCount,
                    MissCount = missCount,
                    LastClearTime = lastClearTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return new CacheStatistics();
            }
        }

        private void OnCacheItemEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (key is string stringKey)
            {
                _keyTracker.TryRemove(stringKey, out _);
                _logger.LogDebug("Cache item evicted: {Key}, Reason: {Reason}", stringKey, reason);
            }
        }

        private string GenerateCacheKey<T>(string key)
        {
            var typeName = typeof(T).Name.ToLowerInvariant();
            return $"repo:{typeName}:{key}";
        }

        private long EstimateMemorySize()
        {
            try
            {
                // This is a rough estimation - in a real scenario, might want to use
                // more sophisticated memory measurement techniques
                return _keyTracker.Count * 100; // Assuming average 100 bytes per cached item
            }
            catch
            {
                return 0;
            }
        }
    }
}
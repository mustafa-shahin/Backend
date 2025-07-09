using Backend.CMS.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Caching
{
    /// <summary>
    /// Memory-based repository cache implementation
    /// </summary>
    public class MemoryRepositoryCache : IRepositoryCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryRepositoryCache> _logger;
        private readonly ConcurrentDictionary<string, HashSet<string>> _taggedKeys;
        private readonly ConcurrentDictionary<string, DateTime> _keyTimestamps;
        private readonly SemaphoreSlim _semaphore;

        public MemoryRepositoryCache(IMemoryCache memoryCache, ILogger<MemoryRepositoryCache> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taggedKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _keyTimestamps = new ConcurrentDictionary<string, DateTime>();
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out var value))
                {
                    if (value is T typedValue)
                    {
                        _logger.LogDebug("Cache hit for key: {Key}", key);
                        return typedValue;
                    }
                    if (value is string jsonValue)
                    {
                        var deserializedValue = JsonSerializer.Deserialize<T>(jsonValue);
                        _logger.LogDebug("Cache hit for key: {Key} (deserialized)", key);
                        return deserializedValue;
                    }
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache item for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) where T : class
        {
            if (value == null) return;

            try
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    Priority = CacheItemPriority.Normal
                };

                // Add eviction callback to clean up tags
                options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
                {
                    EvictionCallback = OnCacheItemEvicted,
                    State = key
                });

                // Store the value (serialize if complex type)
                var cacheValue = ShouldSerialize<T>() ? JsonSerializer.Serialize(value) : (object)value;
                _memoryCache.Set(key, cacheValue, options);

                // Track tags
                if (tags != null)
                {
                    await _semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        foreach (var tag in tags)
                        {
                            _taggedKeys.AddOrUpdate(tag, new HashSet<string> { key }, (_, existing) =>
                            {
                                existing.Add(key);
                                return existing;
                            });
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }

                _keyTimestamps[key] = DateTime.UtcNow;
                _logger.LogDebug("Cache set for key: {Key}, expiration: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache item for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                _memoryCache.Remove(key);
                _keyTimestamps.TryRemove(key, out _);

                // Remove from tag tracking
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    foreach (var taggedKey in _taggedKeys)
                    {
                        taggedKey.Value.Remove(key);
                        if (taggedKey.Value.Count == 0)
                        {
                            _taggedKeys.TryRemove(taggedKey.Key, out _);
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                _logger.LogDebug("Cache removed for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache item for key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var keysToRemove = _keyTimestamps.Keys.Where(key => regex.IsMatch(key)).ToList();

                foreach (var key in keysToRemove)
                {
                    await RemoveAsync(key, cancellationToken);
                }

                _logger.LogDebug("Cache removed by pattern: {Pattern}, keys removed: {Count}", pattern, keysToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache items by pattern: {Pattern}", pattern);
            }
        }

        public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (_taggedKeys.TryGetValue(tag, out var keys))
                    {
                        var keysToRemove = keys.ToList();
                        foreach (var key in keysToRemove)
                        {
                            _memoryCache.Remove(key);
                            _keyTimestamps.TryRemove(key, out _);
                        }

                        _taggedKeys.TryRemove(tag, out _);
                        _logger.LogDebug("Cache removed by tag: {Tag}, keys removed: {Count}", tag, keysToRemove.Count);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache items by tag: {Tag}", tag);
            }
        }

        public async Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            foreach (var tag in tags)
            {
                await RemoveByTagAsync(tag, cancellationToken);
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        {
            // For value types, the default will be 0 or false, etc.
            // For reference types, it will be null.
            T? cachedValue = await GetInternalAsync<T>(key, cancellationToken);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            var value = await factory();
            // We should still check for default/null for value types too if that's a possibility
            if (value != null)
            {
                await SetInternalAsync(key, value, expiration, tags, cancellationToken);
            }

            return value;
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return _memoryCache.TryGetValue(key, out _);
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Clear all tracked keys
                    var keysToRemove = _keyTimestamps.Keys.ToList();
                    foreach (var key in keysToRemove)
                    {
                        _memoryCache.Remove(key);
                    }

                    _keyTimestamps.Clear();
                    _taggedKeys.Clear();
                    _logger.LogInformation("All cache cleared, keys removed: {Count}", keysToRemove.Count);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache");
            }
        }

        private void OnCacheItemEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (key is string keyString)
            {
                _keyTimestamps.TryRemove(keyString, out _);
                _logger.LogDebug("Cache item evicted: {Key}, reason: {Reason}", keyString, reason);
            }
        }
        private async Task<T?> GetInternalAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out var value))
                {
                    if (value is T typedValue)
                    {
                        _logger.LogDebug("Cache hit for key: {Key}", key);
                        return typedValue;
                    }
                    if (value is string jsonValue)
                    {
                        _logger.LogDebug("Cache hit for key: {Key} (deserialized)", key);
                        return JsonSerializer.Deserialize<T>(jsonValue);
                    }
                }
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default(T); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache item for key: {Key}", key);
                return default(T);
            }
        }

        private async Task SetInternalAsync<T>(string key, T value, TimeSpan expiration, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        {
            if (EqualityComparer<T>.Default.Equals(value, default(T)) && !typeof(T).IsClass)
            {
                return;
            }

            try
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    Priority = CacheItemPriority.Normal
                };

                options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
                {
                    EvictionCallback = OnCacheItemEvicted,
                    State = key
                });

                var cacheValue = ShouldSerialize<T>() ? JsonSerializer.Serialize(value) : (object?)value; // Use object? for nullable value types
                _memoryCache.Set(key, cacheValue, options);

                if (tags != null)
                {
                    await _semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        foreach (var tag in tags)
                        {
                            _taggedKeys.AddOrUpdate(tag, new HashSet<string> { key }, (_, existing) =>
                            {
                                existing.Add(key);
                                return existing;
                            });
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }

                _keyTimestamps[key] = DateTime.UtcNow;
                _logger.LogDebug("Cache set for key: {Key}, expiration: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache item for key: {Key}", key);
            }
        }
    
        private static bool ShouldSerialize<T>()
        {
            var type = typeof(T);
            return !type.IsPrimitive &&
                   type != typeof(string) &&
                   type != typeof(decimal) &&
                   type != typeof(DateTime) &&
                   type != typeof(DateTimeOffset) &&
                   type != typeof(TimeSpan) &&
                   type != typeof(Guid);
        }
    }
}
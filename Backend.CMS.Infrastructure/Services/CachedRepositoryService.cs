using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Services
{
    public interface ICachedRepositoryService
    {
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> getItem, TimeSpan? expiration = null) where T : class;
        Task<int?> GetOrSetIntAsync(string key, Func<Task<int?>> getItem, TimeSpan? expiration = null);
        Task<IEnumerable<T>> GetOrSetListAsync<T>(string key, Func<Task<IEnumerable<T>>> getItems, TimeSpan? expiration = null) where T : class;
        void Remove(string key);
        void RemoveByPattern(string pattern);
        void RemoveByTag(string tag);
        void Clear();
        Task<bool> ExistsAsync(string key);
        void SetTags(string key, params string[] tags);
    }

    public class CachedRepositoryService : ICachedRepositoryService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedRepositoryService> _logger;
        private readonly ConcurrentDictionary<string, HashSet<string>> _taggedKeys;
        private readonly ConcurrentDictionary<string, HashSet<string>> _keyTags;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

        public CachedRepositoryService(IMemoryCache cache, ILogger<CachedRepositoryService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taggedKeys = new ConcurrentDictionary<string, HashSet<string>>();
            _keyTags = new ConcurrentDictionary<string, HashSet<string>>();
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> getItem, TimeSpan? expiration = null) where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                if (getItem == null)
                    throw new ArgumentNullException(nameof(getItem));

                // Try to get from cache
                if (_cache.TryGetValue(key, out T? cachedItem))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return cachedItem;
                }

                // Get from data source
                _logger.LogDebug("Cache miss for key: {Key}. Fetching from data source", key);
                var item = await getItem();

                if (item != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                        SlidingExpiration = TimeSpan.FromMinutes(5),
                        Priority = CacheItemPriority.Normal
                    };

                    cacheOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);

                    _cache.Set(key, item, cacheOptions);
                    _logger.LogDebug("Item cached with key: {Key}", key);
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
                return await getItem(); // Fallback to direct data access
            }
        }

        public async Task<int?> GetOrSetIntAsync(string key, Func<Task<int?>> getItem, TimeSpan? expiration = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                if (getItem == null)
                    throw new ArgumentNullException(nameof(getItem));

                if (_cache.TryGetValue(key, out int? cachedItem))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return cachedItem;
                }

                _logger.LogDebug("Cache miss for key: {Key}. Fetching from data source", key);
                var item = await getItem();

                if (item.HasValue)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                        SlidingExpiration = TimeSpan.FromMinutes(5),
                        Priority = CacheItemPriority.Normal
                    };

                    cacheOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);

                    _cache.Set(key, item.Value, cacheOptions);
                    _logger.LogDebug("Item cached with key: {Key}", key);
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetIntAsync for key: {Key}", key);
                return await getItem();
            }
        }

        public async Task<IEnumerable<T>> GetOrSetListAsync<T>(string key, Func<Task<IEnumerable<T>>> getItems, TimeSpan? expiration = null) where T : class
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                if (getItems == null)
                    throw new ArgumentNullException(nameof(getItems));

                // Try to get from cache
                if (_cache.TryGetValue(key, out IEnumerable<T>? cachedItems))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return cachedItems;
                }

                // Get from data source
                _logger.LogDebug("Cache miss for key: {Key}. Fetching from data source", key);
                var items = await getItems();

                if (items != null)
                {
                    var itemList = items.ToList();
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                        SlidingExpiration = TimeSpan.FromMinutes(5),
                        Priority = CacheItemPriority.Normal
                    };

                    cacheOptions.RegisterPostEvictionCallback(OnCacheItemEvicted);

                    _cache.Set(key, itemList, cacheOptions);
                    _logger.LogDebug("Items cached with key: {Key}, count: {Count}", key, itemList.Count);
                }

                return items ?? Enumerable.Empty<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetListAsync for key: {Key}", key);
                return await getItems(); // Fallback to direct data access
            }
        }

        public void Remove(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                _cache.Remove(key);
                RemoveKeyTags(key);
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entry for key: {Key}", key);
            }
        }

        public void RemoveByPattern(string pattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

                // Note: MemoryCache doesn't provide a way to enumerate keys directly
                // In a production environment, consider using a more sophisticated caching solution
                // like Redis that supports pattern-based key removal
                
                var keysToRemove = new List<string>();
                
                // For now, we'll use the tagged keys approach
                var allKeys = _keyTags.Keys.ToList();
                foreach (var key in allKeys)
                {
                    if (IsPatternMatch(key, pattern))
                    {
                        keysToRemove.Add(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    Remove(key);
                }

                _logger.LogDebug("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
            }
        }

        public void RemoveByTag(string tag)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tag))
                    throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

                if (_taggedKeys.TryGetValue(tag, out var keys))
                {
                    var keysToRemove = keys.ToList();
                    foreach (var key in keysToRemove)
                    {
                        Remove(key);
                    }
                    
                    _taggedKeys.TryRemove(tag, out _);
                    _logger.LogDebug("Removed {Count} cache entries with tag: {Tag}", keysToRemove.Count, tag);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by tag: {Tag}", tag);
            }
        }

        public void Clear()
        {
            try
            {
                // Note: MemoryCache doesn't provide a Clear method
                // This is a limitation of IMemoryCache
                _taggedKeys.Clear();
                _keyTags.Clear();
                _logger.LogDebug("Cleared tag tracking dictionaries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                return _cache.TryGetValue(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }

        public void SetTags(string key, params string[] tags)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));

                if (tags == null || tags.Length == 0)
                    return;

                // Remove existing tags for this key
                RemoveKeyTags(key);

                // Add new tags
                var tagSet = new HashSet<string>(tags);
                _keyTags[key] = tagSet;

                foreach (var tag in tags)
                {
                    _taggedKeys.AddOrUpdate(tag, 
                        new HashSet<string> { key }, 
                        (_, existingKeys) => 
                        {
                            existingKeys.Add(key);
                            return existingKeys;
                        });
                }

                _logger.LogDebug("Set tags for key {Key}: {Tags}", key, string.Join(", ", tags));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting tags for key: {Key}", key);
            }
        }

        private void RemoveKeyTags(string key)
        {
            if (_keyTags.TryRemove(key, out var tags))
            {
                foreach (var tag in tags)
                {
                    if (_taggedKeys.TryGetValue(tag, out var keys))
                    {
                        keys.Remove(key);
                        if (keys.Count == 0)
                        {
                            _taggedKeys.TryRemove(tag, out _);
                        }
                    }
                }
            }
        }

        private void OnCacheItemEvicted(object key, object? value, EvictionReason reason, object? state)
        {
            try
            {
                if (key is string stringKey)
                {
                    RemoveKeyTags(stringKey);
                    _logger.LogDebug("Cache item evicted: {Key}, reason: {Reason}", stringKey, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cache eviction callback for key: {Key}", key);
            }
        }

        private static bool IsPatternMatch(string text, string pattern)
        {
            // Simple pattern matching - supports * as wildcard
            if (pattern == "*")
                return true;

            if (pattern.Contains("*"))
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(text, regexPattern);
            }

            return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class CacheKeyBuilder
    {
        public static string BuildEntityKey<T>(int id) => $"{typeof(T).Name}:Id:{id}";
        public static string BuildEntityKey<T>(string identifier) => $"{typeof(T).Name}:Key:{identifier}";
        public static string BuildListKey<T>(string operation, params object[] parameters) => 
            $"{typeof(T).Name}:List:{operation}:{string.Join(":", parameters)}";
        public static string BuildSearchKey<T>(string searchTerm, int page, int pageSize) => 
            $"{typeof(T).Name}:Search:{searchTerm}:{page}:{pageSize}";
        public static string BuildPagedKey<T>(int page, int pageSize) => 
            $"{typeof(T).Name}:Paged:{page}:{pageSize}";
        public static string BuildCountKey<T>(string? filter = null) => 
            $"{typeof(T).Name}:Count:{filter ?? "all"}";
    }
}
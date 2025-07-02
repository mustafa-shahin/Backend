namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Enhanced service for cache operations with cache-aware patterns
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Get cached item by key
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Get cached item by key, or execute function if not found and cache the result
        /// </summary>
        Task<T?> GetAsync<T>(string key, Func<Task<T?>> getItem, bool cacheEmptyCollections = true) where T : class;

        /// <summary>
        /// Set cached item with optional expiration
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Remove cached item by key
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Remove multiple cached items by keys
        /// </summary>
        Task RemoveAsync(IEnumerable<string> keys);

        /// <summary>
        /// Remove cached items matching pattern
        /// </summary>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Check if cached item exists
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Refresh cached item expiration
        /// </summary>
        Task RefreshAsync(string key);

        /// <summary>
        /// Generate cache key from prefix and identifiers
        /// </summary>
        string GenerateKey(string prefix, params object[] identifiers);

        // New cache-aware operations
        /// <summary>
        /// Get or add with automatic cache refresh after factory execution
        /// </summary>
        Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Execute operation and refresh cache with result
        /// </summary>
        Task<T> RefreshCacheAsync<T>(string key, Func<Task<T>> operation, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Execute operation and invalidate related cache patterns
        /// </summary>
        Task<T> InvalidateAndRefreshAsync<T>(Func<Task<T>> operation, params string[] cachePatterns) where T : class;

        /// <summary>
        /// Invalidate cache patterns without executing an operation
        /// </summary>
        Task InvalidatePatternsAsync(params string[] cachePatterns);

        /// <summary>
        /// Batch cache operations for multiple entities
        /// </summary>
        Task SetBatchAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null) where T : class;

        /// <summary>
        /// Get multiple cached items by keys
        /// </summary>
        Task<Dictionary<string, T?>> GetBatchAsync<T>(IEnumerable<string> keys) where T : class;
    }
}
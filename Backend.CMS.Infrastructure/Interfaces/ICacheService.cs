

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service for basic cache operations
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
    }
}
using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
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

    /// <summary>
    /// Service for cache invalidation operations
    /// </summary>
    public interface ICacheInvalidationService
    {
        /// <summary>
        /// Invalidate all cache entries related to a specific user
        /// </summary>
        Task InvalidateUserCacheAsync(int userId);

        /// <summary>
        /// Invalidate all cache entries related to a specific page
        /// </summary>
        Task InvalidatePageCacheAsync(int pageId);
        /// <summary>
        /// Invalidate all company-related cache entries
        /// </summary>
        Task InvalidateCompanyCacheAsync();

        /// <summary>
        /// Invalidate location-related cache entries
        /// </summary>
        Task InvalidateLocationCacheAsync(int? locationId = null);

        /// <summary>
        /// Invalidate file-related cache entries
        /// </summary>
        Task InvalidateFileCacheAsync(int? fileId = null);

        /// <summary>
        /// Invalidate folder-related cache entries
        /// </summary>
        Task InvalidateFolderCacheAsync(int? folderId = null);

        /// <summary>
        /// Invalidate all cache entries (use with caution)
        /// </summary>
        Task InvalidateAllCacheAsync();

        /// <summary>
        /// Warmup cache with commonly accessed data
        /// </summary>
        Task WarmupCacheAsync();

        /// <summary>
        /// Get cache statistics and metrics
        /// </summary>
        Task<Dictionary<string, object>> GetCacheStatisticsAsync();

        /// <summary>
        /// Get all cache keys matching a pattern
        /// </summary>
        Task<List<string>> GetCacheKeysAsync(string pattern = "*");

    }
}
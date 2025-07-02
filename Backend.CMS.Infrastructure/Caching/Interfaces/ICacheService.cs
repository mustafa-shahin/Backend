namespace Backend.CMS.Infrastructure.Caching.Interfaces
{
    /// <summary>
    /// Main caching service interface providing generic cache operations
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Get cached item or add if not exists using factory function
        /// </summary>
        Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Get cached item by key
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Set cached item with optional expiration
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Remove cached item by key
        /// </summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove multiple cached items by keys
        /// </summary>
        Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove cached items matching pattern (Redis SCAN + DELETE)
        /// </summary>
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if cached item exists
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refresh cached item expiration
        /// </summary>
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get multiple cached items by keys
        /// </summary>
        Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Set multiple cached items
        /// </summary>
        Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
    }
}
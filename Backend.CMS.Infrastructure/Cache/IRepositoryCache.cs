namespace Backend.CMS.Infrastructure.Cache
{
    /// <summary>
    /// Repository-level caching interface
    /// </summary>
    public interface IRepositoryCache
    {
        /// <summary>
        /// Gets cached value
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached value or null if not found</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets cached value
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Expiration time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Removes cached value
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes cached values by pattern
        /// </summary>
        /// <param name="pattern">Pattern to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or sets cached value
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create value if not cached</param>
        /// <param name="expiration">Expiration time</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached or newly created value</returns>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Checks if key exists in cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if key exists</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cache
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        Task<CacheStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Cache statistics
    /// </summary>
    public class CacheStatistics
    {
        public int KeyCount { get; set; }
        public long TotalMemorySize { get; set; }
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests * 100 : 0;
        public int TotalRequests => HitCount + MissCount;
        public DateTime LastClearTime { get; set; }
    }
}
namespace Backend.CMS.Infrastructure.Cache
{
    /// <summary>
    /// Repository cache interface
    /// </summary>
    public interface IRepositoryCache
    {
        /// <summary>
        /// Get item from cache
        /// </summary>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Set item in cache
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan expiration, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Remove item from cache
        /// </summary>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove items by pattern
        /// </summary>
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove items by tag
        /// </summary>
        Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove items by multiple tags
        /// </summary>
        Task RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get or set item in cache
        /// </summary>
        // Remove 'where T : class' to allow value types
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all cache
        /// </summary>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
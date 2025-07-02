namespace Backend.CMS.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// Cache monitoring service
    /// </summary>
    public interface ICacheMonitoringService
    {
        /// <summary>
        /// Get current cache statistics
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();

        /// <summary>
        /// Reset statistics counters
        /// </summary>
        Task ResetStatisticsAsync();

        /// <summary>
        /// Get cache health status
        /// </summary>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Get cache size information
        /// </summary>
        Task<Dictionary<string, object>> GetCacheInfoAsync();
    }
}

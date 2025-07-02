namespace Backend.CMS.Infrastructure.Caching.Extensions
{
    public static class CacheProfiles
    {
        /// <summary>
        /// Configuration optimized for development environment
        /// </summary>
        public static CacheOptions Development => new CacheConfigurationBuilder()
            .WithDefaultExpiration(TimeSpan.FromMinutes(10))
            .WithLongExpiration(TimeSpan.FromHours(1))
            .WithShortExpiration(TimeSpan.FromMinutes(2))
            .WithKeyPrefix("dev-cms")
            .WithDistributedLocking(false) // Simpler for development
            .WithRetryPolicy(1, TimeSpan.FromMilliseconds(50))
            .Build();

        /// <summary>
        /// Configuration optimized for production environment
        /// </summary>
        public static CacheOptions Production => new CacheConfigurationBuilder()
            .WithDefaultExpiration(TimeSpan.FromMinutes(30))
            .WithLongExpiration(TimeSpan.FromHours(6))
            .WithShortExpiration(TimeSpan.FromMinutes(5))
            .WithKeyPrefix("cms")
            .WithDistributedLocking(true)
            .WithLockTimeout(TimeSpan.FromSeconds(30))
            .WithRetryPolicy(3, TimeSpan.FromMilliseconds(100))
            .WithCompression(true)
            .Build();

        /// <summary>
        /// Configuration optimized for high-performance scenarios
        /// </summary>
        public static CacheOptions HighPerformance => new CacheConfigurationBuilder()
            .WithDefaultExpiration(TimeSpan.FromMinutes(60))
            .WithLongExpiration(TimeSpan.FromHours(12))
            .WithShortExpiration(TimeSpan.FromMinutes(10))
            .WithKeyPrefix("hperf-cms")
            .WithDistributedLocking(true)
            .WithLockTimeout(TimeSpan.FromSeconds(10)) // Shorter lock timeout
            .WithRetryPolicy(2, TimeSpan.FromMilliseconds(25)) // Faster retries
            .WithCompression(true)
            .Build();

        /// <summary>
        /// Configuration optimized for memory conservation
        /// </summary>
        public static CacheOptions MemoryOptimized => new CacheConfigurationBuilder()
            .WithDefaultExpiration(TimeSpan.FromMinutes(15))
            .WithLongExpiration(TimeSpan.FromHours(2))
            .WithShortExpiration(TimeSpan.FromMinutes(3))
            .WithKeyPrefix("mem-cms")
            .WithDistributedLocking(true)
            .WithCompression(true) // Reduce memory usage
            .WithMaxKeyLength(100) // Shorter keys
            .Build();
    }
}

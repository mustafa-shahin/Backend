using Backend.CMS.Infrastructure.Caching.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Backend.CMS.Infrastructure.Caching
{
    /// <summary>
    /// Extension methods for registering cache services with pre-configured profiles
    /// </summary>
    public static class CacheRegistrationExtensions
    {
        /// <summary>
        /// Add caching with development profile
        /// </summary>
        public static IServiceCollection AddDevelopmentCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var devOptions = CacheProfiles.Development;
                options.DefaultExpiration = devOptions.DefaultExpiration;
                options.LongExpiration = devOptions.LongExpiration;
                options.ShortExpiration = devOptions.ShortExpiration;
                options.KeyPrefix = devOptions.KeyPrefix;
                options.EnableDistributedLocking = devOptions.EnableDistributedLocking;
                options.RetryAttempts = devOptions.RetryAttempts;
                options.RetryDelay = devOptions.RetryDelay;
                options.EnableCompression = devOptions.EnableCompression;
                options.LockTimeout = devOptions.LockTimeout;
                options.MaxKeyLength = devOptions.MaxKeyLength;
            });
        }

        /// <summary>
        /// Add caching with production profile
        /// </summary>
        public static IServiceCollection AddProductionCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var prodOptions = CacheProfiles.Production;
                options.DefaultExpiration = prodOptions.DefaultExpiration;
                options.LongExpiration = prodOptions.LongExpiration;
                options.ShortExpiration = prodOptions.ShortExpiration;
                options.KeyPrefix = prodOptions.KeyPrefix;
                options.EnableDistributedLocking = prodOptions.EnableDistributedLocking;
                options.LockTimeout = prodOptions.LockTimeout;
                options.RetryAttempts = prodOptions.RetryAttempts;
                options.RetryDelay = prodOptions.RetryDelay;
                options.EnableCompression = prodOptions.EnableCompression;
                options.MaxKeyLength = prodOptions.MaxKeyLength;
            });
        }

        /// <summary>
        /// Add caching with high-performance profile
        /// </summary>
        public static IServiceCollection AddHighPerformanceCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var hpOptions = CacheProfiles.HighPerformance;
                options.DefaultExpiration = hpOptions.DefaultExpiration;
                options.LongExpiration = hpOptions.LongExpiration;
                options.ShortExpiration = hpOptions.ShortExpiration;
                options.KeyPrefix = hpOptions.KeyPrefix;
                options.EnableDistributedLocking = hpOptions.EnableDistributedLocking;
                options.LockTimeout = hpOptions.LockTimeout;
                options.RetryAttempts = hpOptions.RetryAttempts;
                options.RetryDelay = hpOptions.RetryDelay;
                options.EnableCompression = hpOptions.EnableCompression;
                options.MaxKeyLength = hpOptions.MaxKeyLength;
            });
        }

        /// <summary>
        /// Add caching with memory-optimized profile
        /// </summary>
        public static IServiceCollection AddMemoryOptimizedCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var memOptions = CacheProfiles.MemoryOptimized;
                options.DefaultExpiration = memOptions.DefaultExpiration;
                options.LongExpiration = memOptions.LongExpiration;
                options.ShortExpiration = memOptions.ShortExpiration;
                options.KeyPrefix = memOptions.KeyPrefix;
                options.EnableDistributedLocking = memOptions.EnableDistributedLocking;
                options.LockTimeout = memOptions.LockTimeout;
                options.RetryAttempts = memOptions.RetryAttempts;
                options.RetryDelay = memOptions.RetryDelay;
                options.EnableCompression = memOptions.EnableCompression;
                options.MaxKeyLength = memOptions.MaxKeyLength;
            });
        }

        /// <summary>
        /// Add caching with custom profile from configuration
        /// </summary>
        public static IServiceCollection AddConfiguredCaching(this IServiceCollection services, IConfiguration configuration, string profileName = "Default")
        {
            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var cacheSection = configuration.GetSection($"CacheProfiles:{profileName}");
                if (cacheSection.Exists())
                {
                    cacheSection.Bind(options);
                }
                else
                {
                    // Fallback to production defaults
                    var prodOptions = CacheProfiles.Production;
                    options.DefaultExpiration = prodOptions.DefaultExpiration;
                    options.LongExpiration = prodOptions.LongExpiration;
                    options.ShortExpiration = prodOptions.ShortExpiration;
                    options.KeyPrefix = prodOptions.KeyPrefix;
                    options.EnableDistributedLocking = prodOptions.EnableDistributedLocking;
                    options.LockTimeout = prodOptions.LockTimeout;
                    options.RetryAttempts = prodOptions.RetryAttempts;
                    options.RetryDelay = prodOptions.RetryDelay;
                    options.EnableCompression = prodOptions.EnableCompression;
                    options.MaxKeyLength = prodOptions.MaxKeyLength;
                }
            });
        }

        /// <summary>
        /// Add caching based on environment
        /// </summary>
        public static IServiceCollection AddEnvironmentBasedCaching(this IServiceCollection services, IConfiguration configuration, string environment)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

            return environment.ToLowerInvariant() switch
            {
                "development" => services.AddDevelopmentCaching(redisConnectionString),
                "production" => services.AddProductionCaching(redisConnectionString),
                "staging" => services.AddHighPerformanceCaching(redisConnectionString),
                _ => services.AddProductionCaching(redisConnectionString)
            };
        }
    }
}
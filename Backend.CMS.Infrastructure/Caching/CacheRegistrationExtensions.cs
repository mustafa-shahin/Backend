using Backend.CMS.Infrastructure.Caching.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching
{
    public static class CacheRegistrationExtensions
    {
        /// <summary>
        /// Add caching with development profile
        /// </summary>
        public static IServiceCollection AddDevelopmentCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var devOptions = Backend.CMS.Infrastructure.Caching.Extensions.CacheProfiles.Development;
                options.DefaultExpiration = devOptions.DefaultExpiration;
                options.LongExpiration = devOptions.LongExpiration;
                options.ShortExpiration = devOptions.ShortExpiration;
                options.KeyPrefix = devOptions.KeyPrefix;
                options.EnableDistributedLocking = devOptions.EnableDistributedLocking;
                options.RetryAttempts = devOptions.RetryAttempts;
                options.RetryDelay = devOptions.RetryDelay;
            });
        }

        /// <summary>
        /// Add caching with production profile
        /// </summary>
        public static IServiceCollection AddProductionCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var prodOptions = Backend.CMS.Infrastructure.Caching.Extensions.CacheProfiles.Production;
                options.DefaultExpiration = prodOptions.DefaultExpiration;
                options.LongExpiration = prodOptions.LongExpiration;
                options.ShortExpiration = prodOptions.ShortExpiration;
                options.KeyPrefix = prodOptions.KeyPrefix;
                options.EnableDistributedLocking = prodOptions.EnableDistributedLocking;
                options.LockTimeout = prodOptions.LockTimeout;
                options.RetryAttempts = prodOptions.RetryAttempts;
                options.RetryDelay = prodOptions.RetryDelay;
                options.EnableCompression = prodOptions.EnableCompression;
            });
        }

        /// <summary>
        /// Add caching with high-performance profile
        /// </summary>
        public static IServiceCollection AddHighPerformanceCaching(this IServiceCollection services, string redisConnectionString)
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                var hpOptions = Backend.CMS.Infrastructure.Caching.Extensions.CacheProfiles.HighPerformance;
                options.DefaultExpiration = hpOptions.DefaultExpiration;
                options.LongExpiration = hpOptions.LongExpiration;
                options.ShortExpiration = hpOptions.ShortExpiration;
                options.KeyPrefix = hpOptions.KeyPrefix;
                options.EnableDistributedLocking = hpOptions.EnableDistributedLocking;
                options.LockTimeout = hpOptions.LockTimeout;
                options.RetryAttempts = hpOptions.RetryAttempts;
                options.RetryDelay = hpOptions.RetryDelay;
                options.EnableCompression = hpOptions.EnableCompression;
            });
        }
    }
}

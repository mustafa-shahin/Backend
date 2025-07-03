using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Backend.CMS.Infrastructure.Caching.Extensions
{
    /// <summary>
    /// Extension methods for registering cache services
    /// </summary>
    public static class CacheServiceExtensions
    {
        /// <summary>
        /// Add Redis caching services with full configuration
        /// </summary>
        public static IServiceCollection AddRedisCaching(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure cache options from configuration
            services.Configure<CacheOptions>(options =>
            {
                var cacheSection = configuration.GetSection("Cache");
                if (cacheSection.Exists())
                {
                    cacheSection.Bind(options);
                }
                else
                {
                    // Use defaults if no configuration section exists
                    options.DefaultExpiration = TimeSpan.FromMinutes(30);
                    options.LongExpiration = TimeSpan.FromHours(6);
                    options.ShortExpiration = TimeSpan.FromMinutes(5);
                    options.KeyPrefix = "cms";
                    options.EnableDistributedLocking = true;
                    options.LockTimeout = TimeSpan.FromSeconds(30);
                    options.RetryAttempts = 3;
                    options.RetryDelay = TimeSpan.FromMilliseconds(100);
                }
            });

            services.AddSingleton<ICacheKeyService, CacheKeyService>();
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ICacheService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheInvalidationService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheMonitoringService>(provider =>
                provider.GetRequiredService<RedisCacheService>());

            // Add Redis distributed cache
            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = configuration["Cache:KeyPrefix"] ?? "cms";
            });

            // Add Redis connection multiplexer
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            });

            // Add background services
            services.AddHostedService<CacheHealthCheckService>();
            services.AddHostedService<CacheStatisticsService>();

            return services;
        }

        /// <summary>
        /// Add Redis caching with custom options
        /// </summary>
        public static IServiceCollection AddRedisCaching(this IServiceCollection services, IConfiguration configuration, Action<CacheOptions> configureOptions)
        {
            services.Configure(configureOptions);

            services.AddSingleton<ICacheKeyService, CacheKeyService>();
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ICacheService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheInvalidationService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheMonitoringService>(provider =>
                provider.GetRequiredService<RedisCacheService>());

            // Add Redis distributed cache
            var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = configuration["Cache:KeyPrefix"] ?? "cms";
            });

            // Add Redis connection multiplexer
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            });

            // Add background services
            services.AddHostedService<CacheHealthCheckService>();
            services.AddHostedService<CacheStatisticsService>();

            return services;
        }

        /// <summary>
        /// Add Redis caching with connection string and optional configuration
        /// </summary>
        public static IServiceCollection AddRedisCaching(this IServiceCollection services, string redisConnectionString, Action<CacheOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<CacheOptions>(options =>
                {
                    // Use default values
                    options.DefaultExpiration = TimeSpan.FromMinutes(30);
                    options.LongExpiration = TimeSpan.FromHours(6);
                    options.ShortExpiration = TimeSpan.FromMinutes(5);
                    options.KeyPrefix = "cms";
                    options.EnableDistributedLocking = true;
                    options.LockTimeout = TimeSpan.FromSeconds(30);
                    options.RetryAttempts = 3;
                    options.RetryDelay = TimeSpan.FromMilliseconds(100);
                });
            }

            services.AddSingleton<ICacheKeyService, CacheKeyService>();
            services.AddSingleton<RedisCacheService>();
            services.AddSingleton<ICacheService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheInvalidationService>(provider =>
                provider.GetRequiredService<RedisCacheService>());
            services.AddSingleton<ICacheMonitoringService>(provider =>
                provider.GetRequiredService<RedisCacheService>());

            // Add Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "cms"; // Default instance name
            });

            // Add Redis connection multiplexer
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);
                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            });

            // Add background services
            services.AddHostedService<CacheHealthCheckService>();
            services.AddHostedService<CacheStatisticsService>();

            return services;
        }

        /// <summary>
        /// Add Redis caching with minimal configuration for testing/development
        /// </summary>
        public static IServiceCollection AddSimpleRedisCaching(this IServiceCollection services, string redisConnectionString = "localhost:6379")
        {
            return services.AddRedisCaching(redisConnectionString, options =>
            {
                options.DefaultExpiration = TimeSpan.FromMinutes(10);
                options.KeyPrefix = "dev-cms";
                options.EnableDistributedLocking = false;
                options.RetryAttempts = 1;
            });
        }
    }
}
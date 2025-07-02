using Backend.CMS.Infrastructure.Caching.Abstractions;
using Backend.CMS.Infrastructure.Caching.Extensions;
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
            // Configure cache options
            services.Configure<CacheOptions>(configuration.GetSection("Cache"));

            // Register core services
            services.AddSingleton<ICacheKeyService, CacheKeyService>();
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<ICacheInvalidationService>(provider =>
                provider.GetRequiredService<ICacheService>() as ICacheInvalidationService ??
                throw new InvalidOperationException("Cache service must implement ICacheInvalidationService"));
            services.AddSingleton<ICacheMonitoringService>(provider =>
                provider.GetRequiredService<ICacheService>() as ICacheMonitoringService ??
                throw new InvalidOperationException("Cache service must implement ICacheMonitoringService"));

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
        public static IServiceCollection AddRedisCaching(this IServiceCollection services, Action<CacheOptions> configureOptions)
        {
            services.Configure(configureOptions);
            return services.AddRedisCaching(Microsoft.Extensions.Configuration.ConfigurationBinder.GetConfiguration());
        }

        /// <summary>
        /// Add Redis caching with connection string
        /// </summary>
        public static IServiceCollection AddRedisCaching(this IServiceCollection services, string redisConnectionString, Action<CacheOptions>? configureOptions = null)
        {
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }
            else
            {
                services.Configure<CacheOptions>(options => { }); // Use defaults
            }

            // Register core services
            services.AddSingleton<ICacheKeyService, CacheKeyService>();
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<ICacheInvalidationService>(provider =>
                provider.GetRequiredService<ICacheService>() as ICacheInvalidationService ??
                throw new InvalidOperationException("Cache service must implement ICacheInvalidationService"));
            services.AddSingleton<ICacheMonitoringService>(provider =>
                provider.GetRequiredService<ICacheService>() as ICacheMonitoringService ??
                throw new InvalidOperationException("Cache service must implement ICacheMonitoringService"));

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
    }
}

using Backend.CMS.Caching.Services;
using Backend.CMS.Interfaces.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Backend.CMS.Caching.Extensions
{
    public static class CachingServiceExtensions
    {
        public static IServiceCollection AddCaching(this IServiceCollection services, string? redisConnectionString = null)
        {
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(redisConnectionString));
                services.AddScoped<ICacheService, RedisCacheService>();
            }
            else
            {
                services.AddMemoryCache();
                services.AddScoped<ICacheService, MemoryCacheService>();
            }

            return services;
        }
    }
}
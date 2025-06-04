using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Caching.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly HashSet<string> _cacheKeys = new();
        private readonly object _lock = new();

        public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            var value = _cache.Get<T>(key);
            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            var options = new MemoryCacheEntryOptions();
            if (expiry.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiry;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            }

            options.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                lock (_lock)
                {
                    _cacheKeys.Remove(k.ToString()!);
                }
            });

            _cache.Set(key, value, options);

            lock (_lock)
            {
                _cacheKeys.Add(key);
            }

            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiry = null) where T : class
        {
            var value = await GetAsync<T>(key);
            if (value != null)
            {
                return value;
            }

            value = await getItem();
            if (value != null)
            {
                await SetAsync(key, value, expiry);
            }

            return value;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            lock (_lock)
            {
                _cacheKeys.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            lock (_lock)
            {
                var keysToRemove = _cacheKeys.Where(k => k.Contains(pattern)).ToList();
                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                    _cacheKeys.Remove(key);
                }
            }
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            lock (_lock)
            {
                foreach (var key in _cacheKeys.ToList())
                {
                    _cache.Remove(key);
                }
                _cacheKeys.Clear();
            }
            return Task.CompletedTask;
        }
    }
}

using Backend.CMS.Application.Common;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class CacheService : ICacheService, ICacheInvalidationService, IDisposable
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CacheService> _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly TimeSpan _longExpiration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _invalidationSemaphore;
        private readonly Dictionary<string, SemaphoreSlim> _keySemaphores;
        private readonly object _semaphoreLock = new object();
        private bool _disposed = false;

        public CacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer connectionMultiplexer,
            IConfiguration configuration,
            ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _defaultExpiration = TimeSpan.FromMinutes(GetConfigValue("CacheSettings:DefaultExpirationMinutes", 30));
            _longExpiration = TimeSpan.FromHours(GetConfigValue("CacheSettings:LongExpirationHours", 24));
            _invalidationSemaphore = new SemaphoreSlim(1, 1);
            _keySemaphores = new Dictionary<string, SemaphoreSlim>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
        }

        #region Basic Cache Operations

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                    return null;
                }

                var result = JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached item with key: {Key}", key);
                await RemoveAsync(key); 
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached item with key: {Key}", key);
                return null;
            }
        }

        public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> getItem, bool cacheEmptyCollections = true) where T : class
        {
            var cachedItem = await GetAsync<T>(key);
            if (cachedItem != null)
                return cachedItem;

            // Use semaphore to prevent cache stampede
            var semaphore = GetKeySemaphore(key);
            await semaphore.WaitAsync();
            try
            {
                // Double-check pattern
                cachedItem = await GetAsync<T>(key);
                if (cachedItem != null)
                    return cachedItem;

                var item = await getItem();
                if (item != null)
                {
                    bool shouldCache = true;

                    if (!cacheEmptyCollections && item is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                    {
                        shouldCache = false;
                    }

                    if (shouldCache)
                    {
                        await SetAsync(key, item);
                    }
                }

                return item;
            }
            finally
            {
                semaphore.Release();
                ReleaseKeySemaphore(key);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
                };

                await _distributedCache.SetStringAsync(key, serializedValue, options);
                _logger.LogDebug("Cache item set with key: {Key}, expiration: {Expiration}",
                    key, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached item with key: {Key}", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                await _distributedCache.RemoveAsync(key);
                _logger.LogDebug("Cache item removed with key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached item with key: {Key}", key);
            }
        }

        public async Task RemoveAsync(IEnumerable<string> keys)
        {
            if (keys?.Any() != true) return;

            var keysList = keys.ToList();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var redisKeys = keysList.Select(k => new RedisKey(k)).ToArray();

                if (redisKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(redisKeys);
                    _logger.LogDebug("Removed {Count} cache items", redisKeys.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cached items");
                // Fallback to individual removal
                var tasks = keysList.Select(RemoveAsync);
                await Task.WhenAll(tasks);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;

            await _invalidationSemaphore.WaitAsync();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var endpoints = _connectionMultiplexer.GetEndPoints();

                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available for pattern deletion");
                    return;
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                const int batchSize = 1000;
                var deletedCount = 0;

                await foreach (var keyBatch in GetKeysBatched(server, pattern, batchSize))
                {
                    if (keyBatch.Any())
                    {
                        await database.KeyDeleteAsync(keyBatch.ToArray());
                        deletedCount += keyBatch.Count();
                    }
                }

                _logger.LogInformation("Removed {Count} cache items matching pattern: {Pattern}",
                    deletedCount, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached items by pattern: {Pattern}", pattern);
            }
            finally
            {
                _invalidationSemaphore.Release();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                return await database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if cached item exists with key: {Key}", key);
                return false;
            }
        }

        public async Task RefreshAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                await _distributedCache.RefreshAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cached item with key: {Key}", key);
            }
        }

        public string GenerateKey(string prefix, params object[] identifiers)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));

            var keyParts = new List<string> { prefix };
            keyParts.AddRange(identifiers.Select(id => id?.ToString() ?? "null"));
            return string.Join(":", keyParts);
        }

        #endregion

        #region Cache Invalidation Methods

        public async Task InvalidateUserCacheAsync(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    // Invalidate all user caches
                    await RemoveByPatternAsync(CacheKeys.UsersPattern);
                    await RemoveByPatternAsync(CacheKeys.Pattern(CacheKeys.USER_PREFIX));
                }
                else
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.UserById(userId),
                        CacheKeys.UserPermissions(userId),
                        CacheKeys.UserSessions(userId)
                    };

                    await RemoveAsync(keysToRemove);
                    await RemoveByPatternAsync($"user:*:{userId}");
                }

                _logger.LogInformation("Invalidated cache for user {UserId}", userId <= 0 ? "all" : userId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user cache for user {UserId}", userId);
            }
        }

        public async Task InvalidatePageCacheAsync(int pageId)
        {
            try
            {
                if (pageId <= 0)
                {
                    // Invalidate all page caches
                    await RemoveByPatternAsync(CacheKeys.PagesPattern);
                    await RemoveByPatternAsync(CacheKeys.DesignerPattern);
                    await RemoveByPatternAsync("preview:*");
                }
                else
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.PageById(pageId),
                        CacheKeys.PageWithComponents(pageId),
                        CacheKeys.PageVersions(pageId),
                        CacheKeys.DesignerPage(pageId)
                    };

                    await RemoveAsync(keysToRemove);
                    await RemoveByPatternAsync($"page:*:{pageId}");
                    await RemoveByPatternAsync($"designer:*:{pageId}");
                }

                // Always invalidate these when any page changes
                var globalKeysToRemove = new List<string>
                {
                    CacheKeys.PublishedPages,
                    CacheKeys.PageHierarchy
                };
                await RemoveAsync(globalKeysToRemove);

                _logger.LogInformation("Invalidated cache for page {PageId}", pageId <= 0 ? "all" : pageId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating page cache for page {PageId}", pageId);
            }
        }

        public async Task InvalidateComponentCacheAsync()
        {
            try
            {
                await RemoveByPatternAsync(CacheKeys.ComponentsPattern);
                await RemoveAsync(CacheKeys.ComponentLibrary);

                _logger.LogInformation("Invalidated all component cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating component cache");
            }
        }

        public async Task InvalidateCompanyCacheAsync()
        {
            try
            {
                await RemoveByPatternAsync(CacheKeys.CompanyPattern);
                _logger.LogInformation("Invalidated company cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating company cache");
            }
        }

        public async Task InvalidateLocationCacheAsync(int? locationId = null)
        {
            try
            {
                if (locationId.HasValue && locationId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.LocationById(locationId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.MainLocation
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.LocationsPattern);

                _logger.LogInformation("Invalidated location cache {LocationId}",
                    locationId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating location cache");
            }
        }

        public async Task InvalidateFileCacheAsync(int? fileId = null)
        {
            try
            {
                if (fileId.HasValue && fileId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.FileById(fileId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.RecentFiles,
                    CacheKeys.FileStatistics
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.FilesPattern);

                _logger.LogInformation("Invalidated file cache {FileId}",
                    fileId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating file cache");
            }
        }

        public async Task InvalidateFolderCacheAsync(int? folderId = null)
        {
            try
            {
                if (folderId.HasValue && folderId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.FolderById(folderId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.FolderTree
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.FoldersPattern);

                _logger.LogInformation("Invalidated folder cache {FolderId}",
                    folderId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating folder cache");
            }
        }

        public async Task InvalidateAllCacheAsync()
        {
            await _invalidationSemaphore.WaitAsync();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var endpoints = _connectionMultiplexer.GetEndPoints();

                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available for cache invalidation");
                    return;
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());

                await database.ExecuteAsync("FLUSHDB");

                _logger.LogInformation("Invalidated all cache entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all cache");
                throw;
            }
            finally
            {
                _invalidationSemaphore.Release();
            }
        }

        public async Task WarmupCacheAsync()
        {
            try
            {
                _logger.LogInformation("Starting cache warmup...");

                var tasks = new List<Task>
                {
                    WarmupCompanyDataAsync(),
                    WarmupPublishedPagesAsync(),
                    WarmupComponentLibraryAsync(),
                    WarmupMainLocationAsync(),
                    WarmupPermissionsAsync()
                };

                await Task.WhenAll(tasks);

                _logger.LogInformation("Cache warmup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetCacheStatisticsAsync()
        {
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var endpoints = _connectionMultiplexer.GetEndPoints();

                if (!endpoints.Any())
                {
                    return new Dictionary<string, object> { ["Error"] = "No Redis endpoints available" };
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                var info = await server.InfoAsync();

                var statistics = new Dictionary<string, object>();

                // Get basic Redis info
                var infoDict = info.SelectMany(g => g).ToDictionary(kv => kv.Key, kv => kv.Value);

                statistics["MemoryUsed"] = infoDict.GetValueOrDefault("used_memory_human", "Unknown");
                statistics["KeyspaceHits"] = long.Parse(infoDict.GetValueOrDefault("keyspace_hits", "0"));
                statistics["KeyspaceMisses"] = long.Parse(infoDict.GetValueOrDefault("keyspace_misses", "0"));
                statistics["ConnectedClients"] = infoDict.GetValueOrDefault("connected_clients", "0");
                statistics["Uptime"] = infoDict.GetValueOrDefault("uptime_in_seconds", "0");

                // Calculate hit ratio
                var hits = (long)statistics["KeyspaceHits"];
                var misses = (long)statistics["KeyspaceMisses"];
                var total = hits + misses;
                statistics["HitRatio"] = total > 0 ? Math.Round((double)hits / total * 100, 2) : 0;

                // Get key counts by pattern
                statistics["TotalKeys"] = await GetKeyCountAsync(server, "*");
                statistics["UserCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.UsersPattern);
                statistics["PageCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.PagesPattern);
                statistics["ComponentCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.ComponentsPattern);
                statistics["FileCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.FilesPattern);
                statistics["SessionCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.SessionsPattern);

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return new Dictionary<string, object> { ["Error"] = ex.Message };
            }
        }

        public async Task<List<string>> GetCacheKeysAsync(string pattern = "*")
        {
            try
            {
                var endpoints = _connectionMultiplexer.GetEndPoints();
                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available");
                    return new List<string>();
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                var keys = new List<string>();

                await foreach (var keyBatch in GetKeysBatched(server, pattern, 1000))
                {
                    keys.AddRange(keyBatch.Select(k => k.ToString()));
                }

                return keys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache keys with pattern: {Pattern}", pattern);
                return new List<string>();
            }
        }

        #endregion

        #region Warmup Methods

        private async Task WarmupCompanyDataAsync()
        {
            try
            {
                // This would typically fetch company data and cache it
                _logger.LogDebug("Warming up company data cache");
                // Implementation would depend on your specific services
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup company data cache");
            }
        }

        private async Task WarmupPublishedPagesAsync()
        {
            try
            {
                _logger.LogDebug("Warming up published pages cache");
                // Implementation would depend on your specific services
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup published pages cache");
            }
        }

        private async Task WarmupComponentLibraryAsync()
        {
            try
            {
                _logger.LogDebug("Warming up component library cache");
                // Implementation would depend on your specific services
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup component library cache");
            }
        }

        private async Task WarmupMainLocationAsync()
        {
            try
            {
                _logger.LogDebug("Warming up main location cache");
                // Implementation would depend on your specific services
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup main location cache");
            }
        }

        private async Task WarmupPermissionsAsync()
        {
            try
            {
                _logger.LogDebug("Warming up permissions cache");
                // Implementation would depend on your specific services
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup permissions cache");
            }
        }

        #endregion

        #region Helper Methods

        private async IAsyncEnumerable<IEnumerable<RedisKey>> GetKeysBatched(IServer server, string pattern, int batchSize)
        {
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);

                if (keys.Count >= batchSize)
                {
                    yield return keys.ToArray();
                    keys.Clear();
                }
            }

            if (keys.Any())
            {
                yield return keys.ToArray();
            }
        }

        private async Task<long> GetKeyCountAsync(IServer server, string pattern)
        {
            long count = 0;
            await foreach (var _ in server.KeysAsync(pattern: pattern))
            {
                count++;
            }
            return count;
        }

        private SemaphoreSlim GetKeySemaphore(string key)
        {
            lock (_semaphoreLock)
            {
                if (!_keySemaphores.TryGetValue(key, out var semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    _keySemaphores[key] = semaphore;
                }
                return semaphore;
            }
        }

        private void ReleaseKeySemaphore(string key)
        {
            lock (_semaphoreLock)
            {
                if (_keySemaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount > 0)
                {
                    _keySemaphores.Remove(key);
                    semaphore.Dispose();
                }
            }
        }

        private int GetConfigValue(string key, int defaultValue)
        {
            return int.TryParse(_configuration[key], out var value) ? value : defaultValue;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _invalidationSemaphore?.Dispose();

                lock (_semaphoreLock)
                {
                    foreach (var semaphore in _keySemaphores.Values)
                    {
                        semaphore.Dispose();
                    }
                    _keySemaphores.Clear();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
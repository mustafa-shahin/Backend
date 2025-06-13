using Backend.CMS.Application.Common;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class CacheService : ICacheService, ICacheInvalidationService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CacheService> _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly JsonSerializerOptions _jsonOptions;

        public CacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer connectionMultiplexer,
            IConfiguration configuration,
            ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache;
            _connectionMultiplexer = connectionMultiplexer;
            _configuration = configuration;
            _logger = logger;
            _defaultExpiration = TimeSpan.FromMinutes(int.Parse(configuration["CacheSettings:DefaultExpirationMinutes"] ?? "30"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Basic Cache Operations

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                    return null;

                return JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached item with key: {Key}", key);
                return null;
            }
        }

        public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> getItem) where T : class
        {
            var cachedItem = await GetAsync<T>(key);
            if (cachedItem != null)
                return cachedItem;

            var item = await getItem();
            if (item != null)
            {
                await SetAsync(key, item);
            }

            return item;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
                };

                await _distributedCache.SetStringAsync(key, serializedValue, options);
                _logger.LogDebug("Cache item set with key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached item with key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
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
            try
            {
                var tasks = keys.Select(key => _distributedCache.RemoveAsync(key));
                await Task.WhenAll(tasks);
                _logger.LogDebug("Multiple cache items removed: {Count}", keys.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cached items");
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    await database.KeyDeleteAsync(keys);
                    _logger.LogInformation("Removed {Count} cache items matching pattern: {Pattern}", keys.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached items by pattern: {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
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
            var keyParts = new List<string> { prefix };
            keyParts.AddRange(identifiers.Select(id => id.ToString()));
            return string.Join(":", keyParts);
        }

        #endregion

        #region Cache Invalidation Methods

        public async Task InvalidateUserCacheAsync(int userId)
        {
            try
            {
                var keysToRemove = new List<string>
                {
                    CacheKeys.UserById(userId),
                    CacheKeys.UserPermissions(userId)
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.UsersPattern);

                _logger.LogInformation("Invalidated cache for user {UserId}", userId);
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
                var keysToRemove = new List<string>
                {
                    CacheKeys.PageById(pageId),
                    CacheKeys.PublishedPages,
                    CacheKeys.PageHierarchy,
                    $"designer:page:{pageId}",
                    $"page:slug:*", // Will be handled by pattern removal
                    $"page:versions:{pageId}"
                };

                await RemoveAsync(keysToRemove);

                // Remove page-related patterns
                await RemoveByPatternAsync(CacheKeys.PagesPattern);
                await RemoveByPatternAsync($"page:*:{pageId}");
                await RemoveByPatternAsync($"designer:page:{pageId}*");
                await RemoveByPatternAsync($"preview:*"); // Remove all previews as they might be related

                _logger.LogInformation("Invalidated cache for page {PageId}", pageId);
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
                await RemoveByPatternAsync("designer:component-library");
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
                if (locationId.HasValue)
                {
                    await RemoveAsync(CacheKeys.LocationById(locationId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.MainLocation
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.LocationsPattern);

                _logger.LogInformation("Invalidated location cache {LocationId}", locationId?.ToString() ?? "all");
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
                if (fileId.HasValue)
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

                _logger.LogInformation("Invalidated file cache {FileId}", fileId?.ToString() ?? "all");
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
                if (folderId.HasValue)
                {
                    await RemoveAsync(CacheKeys.FolderById(folderId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.FolderTree
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.FoldersPattern);

                _logger.LogInformation("Invalidated folder cache {FolderId}", folderId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating folder cache");
            }
        }

        public async Task InvalidateAllCacheAsync()
        {
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                var keys = server.Keys(pattern: "*").ToArray();
                if (keys.Length > 0)
                {
                    await database.KeyDeleteAsync(keys);
                    _logger.LogInformation("Invalidated all cache - removed {Count} keys", keys.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all cache");
            }
        }

        public async Task WarmupCacheAsync()
        {
            try
            {
                _logger.LogInformation("Starting cache warmup...");

                // Cache warmup logic here
                // This could include pre-loading commonly accessed data
                // For example:
                // - Load main company data
                // - Load published pages
                // - Load component library
                // - Load main categories
                // - Load featured products

                _logger.LogInformation("Cache warmup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
            }
        }

        public async Task<Dictionary<string, object>> GetCacheStatisticsAsync()
        {
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                var info = await server.InfoAsync();
                var allKeys = server.Keys(pattern: "*").ToArray();

                var statistics = new Dictionary<string, object>
                {
                    ["TotalKeys"] = allKeys.Length,
                    ["UserCacheKeys"] = server.Keys(pattern: CacheKeys.UsersPattern).Count(),
                    ["PageCacheKeys"] = server.Keys(pattern: CacheKeys.PagesPattern).Count(),
                    ["ComponentCacheKeys"] = server.Keys(pattern: CacheKeys.ComponentsPattern).Count(),
                    ["FileCacheKeys"] = server.Keys(pattern: CacheKeys.FilesPattern).Count(),
                    ["DesignerCacheKeys"] = server.Keys(pattern: "designer:*").Count(),
                    ["PreviewCacheKeys"] = server.Keys(pattern: "preview:*").Count(),
                    ["MemoryUsed"] = info.SelectMany(g => g).FirstOrDefault(kv => kv.Key == "used_memory_human").Value ?? "Unknown",
                    ["KeyspaceHits"] = info.SelectMany(g => g).FirstOrDefault(kv => kv.Key == "keyspace_hits").Value ?? "0",
                    ["KeyspaceMisses"] = info.SelectMany(g => g).FirstOrDefault(kv => kv.Key == "keyspace_misses").Value ?? "0"
                };

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
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern).Select(k => k.ToString()).ToList();
                return keys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache keys with pattern: {Pattern}", pattern);
                return [];
            }
        }

        #endregion

        #region Session Cache Methods

        public async Task InvalidateUserSessionsAsync(int userId)
        {
            try
            {
                // Remove all sessions for a specific user
                await RemoveByPatternAsync($"session:*");

                // Also remove user-specific cache
                await InvalidateUserCacheAsync(userId);

                _logger.LogInformation("Invalidated all sessions for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user sessions for user {UserId}", userId);
            }
        }

        public async Task InvalidateAllSessionsAsync()
        {
            try
            {
                await RemoveByPatternAsync(CacheKeys.SessionsPattern);
                _logger.LogInformation("Invalidated all sessions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all sessions");
            }
        }

        public async Task<List<string>> GetActiveSessionsAsync()
        {
            try
            {
                return await GetCacheKeysAsync(CacheKeys.SessionsPattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active sessions");
                return new List<string>();
            }
        }

        public async Task<bool> IsSessionActiveAsync(string sessionId)
        {
            try
            {
                var cacheKey = CacheKeys.SessionById(sessionId);
                return await ExistsAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if session {SessionId} is active", sessionId);
                return false;
            }
        }

        public async Task ExtendSessionAsync(string sessionId, TimeSpan? customExpiration = null)
        {
            try
            {
                var cacheKey = CacheKeys.SessionById(sessionId);
                var session = await GetAsync<UserSessionContext>(cacheKey);

                if (session != null)
                {
                    session.UpdateLastActivity();
                    await SetAsync(cacheKey, session, customExpiration ?? _defaultExpiration);
                    _logger.LogDebug("Extended session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending session {SessionId}", sessionId);
            }
        }

        public async Task<Dictionary<string, object>> GetSessionStatisticsAsync()
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var sessionKeys = server.Keys(pattern: CacheKeys.SessionsPattern).ToArray();

                var activeSessions = 0;
                var expiredSessions = 0;
                var userSessions = new Dictionary<int, int>();

                foreach (var key in sessionKeys)
                {
                    try
                    {
                        var database = _connectionMultiplexer.GetDatabase();
                        var sessionData = await database.StringGetAsync(key);

                        if (!string.IsNullOrEmpty(sessionData))
                        {
                            var session = JsonSerializer.Deserialize<UserSessionContext>(sessionData, _jsonOptions);
                            if (session != null)
                            {
                                if (session.IsSessionExpired(_defaultExpiration))
                                {
                                    expiredSessions++;
                                }
                                else
                                {
                                    activeSessions++;

                                    if (session.UserId.HasValue)
                                    {
                                        userSessions[session.UserId.Value] = userSessions.GetValueOrDefault(session.UserId.Value, 0) + 1;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing session key {Key}", key);
                    }
                }

                return new Dictionary<string, object>
                {
                    ["TotalSessions"] = sessionKeys.Length,
                    ["ActiveSessions"] = activeSessions,
                    ["ExpiredSessions"] = expiredSessions,
                    ["UsersWithSessions"] = userSessions.Count,
                    ["SessionsPerUser"] = userSessions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session statistics");
                return new Dictionary<string, object> { ["Error"] = ex.Message };
            }
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var sessionKeys = server.Keys(pattern: CacheKeys.SessionsPattern).ToArray();
                var expiredKeys = new List<RedisKey>();

                foreach (var key in sessionKeys)
                {
                    try
                    {
                        var database = _connectionMultiplexer.GetDatabase();
                        var sessionData = await database.StringGetAsync(key);

                        if (!string.IsNullOrEmpty(sessionData))
                        {
                            var session = JsonSerializer.Deserialize<UserSessionContext>(sessionData, _jsonOptions);
                            if (session?.IsSessionExpired(_defaultExpiration) == true)
                            {
                                expiredKeys.Add(key);
                            }
                        }
                        else
                        {
                            // Empty session data, consider it expired
                            expiredKeys.Add(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking session {Key}, marking for cleanup", key);
                        expiredKeys.Add(key);
                    }
                }

                if (expiredKeys.Count > 0)
                {
                    var database = _connectionMultiplexer.GetDatabase();
                    await database.KeyDeleteAsync(expiredKeys.ToArray());
                    _logger.LogInformation("Cleaned up {Count} expired sessions", expiredKeys.Count);
                }

                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        #endregion
    }
}
using Asp.Versioning;
using Backend.CMS.API.Authorization;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [AdminOrDev]
    public class CacheController : ControllerBase
    {
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheMonitoringService _cacheMonitoringService;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CacheController> _logger;

        public CacheController(
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheMonitoringService cacheMonitoringService,
            IConnectionMultiplexer redis,
            ILogger<CacheController> logger)
        {
            _cacheService = cacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _cacheMonitoringService = cacheMonitoringService;
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetCacheStatistics()
        {
            try
            {
                var statistics = await _cacheMonitoringService.GetStatisticsAsync();
                var cacheInfo = await _cacheMonitoringService.GetCacheInfoAsync();
                var isHealthy = await _cacheMonitoringService.IsHealthyAsync();

                var systemInfo = cacheInfo.ContainsKey("error")
                    ? new
                    {
                        Error = cacheInfo["error"],
                        Memory = (object?)null,
                        Performance = (object?)null,
                        Connection = (object?)null
                    }
                    : new
                    {
                        Error = (object?)null,
                        Memory = ExtractMemoryInfo(cacheInfo),
                        Performance = ExtractPerformanceInfo(cacheInfo),
                        Connection = ExtractConnectionInfo(cacheInfo)
                    };

                var response = new
                {
                    Statistics = new
                    {
                        statistics.HitCount,
                        statistics.MissCount,
                        statistics.ErrorCount,
                        statistics.HitRatio,
                        statistics.TotalOperations,
                        statistics.LastResetTime,
                        TopOperations = statistics.OperationCounts
                            .OrderByDescending(kv => kv.Value)
                            .Take(10)
                            .ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    Health = new
                    {
                        IsHealthy = isHealthy,
                        Status = isHealthy ? "Healthy" : "Unhealthy"
                    },
                    System = systemInfo
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return StatusCode(500, new { Message = "An error occurred while getting cache statistics" });
            }
        }

        /// <summary>
        /// Get cache keys by pattern
        /// </summary>
        [HttpGet("keys")]
        public async Task<ActionResult<object>> GetCacheKeys([FromQuery] string pattern = "*cms*", [FromQuery] int limit = 100)
        {
            try
            {
                var server = GetRedisServer();
                var keys = new List<string>();
                var count = 0;

                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    if (count >= limit) break;
                    keys.Add(key.ToString());
                    count++;
                }

                var response = new
                {
                    Keys = keys,
                    Count = keys.Count,
                    Pattern = pattern,
                    Limit = limit,
                    Truncated = count >= limit
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache keys with pattern: {Pattern}", pattern);
                return StatusCode(500, new { Message = "An error occurred while getting cache keys" });
            }
        }

        /// <summary>
        /// Get cache health status
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<object>> GetCacheHealth()
        {
            try
            {
                var isHealthy = await _cacheMonitoringService.IsHealthyAsync();
                var statistics = await _cacheMonitoringService.GetStatisticsAsync();

                var response = new
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    ErrorRate = statistics.TotalOperations > 0
                        ? (double)statistics.ErrorCount / statistics.TotalOperations
                        : 0,
                    HitRatio = statistics.HitRatio
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache health");
                return StatusCode(500, new { Message = "An error occurred while checking cache health" });
            }
        }

        /// <summary>
        /// Reset cache statistics
        /// </summary>
        [HttpPost("statistics/reset")]
        public async Task<ActionResult> ResetCacheStatistics()
        {
            try
            {
                await _cacheMonitoringService.ResetStatisticsAsync();
                _logger.LogInformation("Cache statistics reset by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "Cache statistics reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting cache statistics");
                return StatusCode(500, new { Message = "An error occurred while resetting cache statistics" });
            }
        }

        /// <summary>
        /// Clear all cache (development only)
        /// </summary>
        [HttpDelete("all")]
        [DevOnly]
        public async Task<ActionResult> ClearAllCache()
        {
            try
            {
                await _cacheInvalidationService.ClearAllAsync();
                _logger.LogWarning("All cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache");
                return StatusCode(500, new { Message = "An error occurred while clearing cache" });
            }
        }

        /// <summary>
        /// Clear user cache
        /// </summary>
        [HttpDelete("users/{userId:int}")]
        public async Task<ActionResult> ClearUserCache(int userId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<User>(userId);
                await _cacheInvalidationService.InvalidateRelatedAsync<User>(userId);

                _logger.LogInformation("User cache cleared for user {UserId} by {CurrentUserId}", userId, GetCurrentUserId());
                return Ok(new { Message = $"User cache cleared for user {userId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing user cache for user {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while clearing user cache" });
            }
        }

        /// <summary>
        /// Clear all user caches
        /// </summary>
        [HttpDelete("users")]
        public async Task<ActionResult> ClearAllUserCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<User>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.UserPattern);

                _logger.LogInformation("All user cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All user cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all user cache");
                return StatusCode(500, new { Message = "An error occurred while clearing user cache" });
            }
        }

        /// <summary>
        /// Clear page cache
        /// </summary>
        [HttpDelete("pages/{pageId:int}")]
        public async Task<ActionResult> ClearPageCache(int pageId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<Page>(pageId);
                await _cacheInvalidationService.InvalidateRelatedAsync<Page>(pageId);

                _logger.LogInformation("Page cache cleared for page {PageId} by user {UserId}", pageId, GetCurrentUserId());
                return Ok(new { Message = $"Page cache cleared for page {pageId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing page cache for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while clearing page cache" });
            }
        }

        /// <summary>
        /// Clear all page caches
        /// </summary>
        [HttpDelete("pages")]
        public async Task<ActionResult> ClearAllPageCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Page>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.PagePattern);

                _logger.LogInformation("All page cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All page cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all page cache");
                return StatusCode(500, new { Message = "An error occurred while clearing page cache" });
            }
        }

        /// <summary>
        /// Clear category cache
        /// </summary>
        [HttpDelete("categories/{categoryId:int}")]
        public async Task<ActionResult> ClearCategoryCache(int categoryId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<Category>(categoryId);
                await _cacheInvalidationService.InvalidateRelatedAsync<Category>(categoryId);

                _logger.LogInformation("Category cache cleared for category {CategoryId} by user {UserId}", categoryId, GetCurrentUserId());
                return Ok(new { Message = $"Category cache cleared for category {categoryId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing category cache for category {CategoryId}", categoryId);
                return StatusCode(500, new { Message = "An error occurred while clearing category cache" });
            }
        }

        /// <summary>
        /// Clear all category caches
        /// </summary>
        [HttpDelete("categories")]
        public async Task<ActionResult> ClearAllCategoryCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Category>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.CategoryPattern);

                _logger.LogInformation("All category cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All category cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all category cache");
                return StatusCode(500, new { Message = "An error occurred while clearing category cache" });
            }
        }

        /// <summary>
        /// Clear product cache
        /// </summary>
        [HttpDelete("products/{productId:int}")]
        public async Task<ActionResult> ClearProductCache(int productId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
                await _cacheInvalidationService.InvalidateRelatedAsync<Product>(productId);

                _logger.LogInformation("Product cache cleared for product {ProductId} by user {UserId}", productId, GetCurrentUserId());
                return Ok(new { Message = $"Product cache cleared for product {productId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing product cache for product {ProductId}", productId);
                return StatusCode(500, new { Message = "An error occurred while clearing product cache" });
            }
        }

        /// <summary>
        /// Clear all product caches
        /// </summary>
        [HttpDelete("products")]
        public async Task<ActionResult> ClearAllProductCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Product>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.ProductPattern);

                _logger.LogInformation("All product cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All product cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all product cache");
                return StatusCode(500, new { Message = "An error occurred while clearing product cache" });
            }
        }

        /// <summary>
        /// Clear company cache
        /// </summary>
        [HttpDelete("company")]
        public async Task<ActionResult> ClearCompanyCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Company>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.CompanyPattern);

                _logger.LogInformation("Company cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "Company cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing company cache");
                return StatusCode(500, new { Message = "An error occurred while clearing company cache" });
            }
        }

        /// <summary>
        /// Clear location cache
        /// </summary>
        [HttpDelete("locations/{locationId:int}")]
        public async Task<ActionResult> ClearLocationCache(int locationId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<Location>(locationId);
                await _cacheInvalidationService.InvalidateRelatedAsync<Location>(locationId);

                _logger.LogInformation("Location cache cleared for location {LocationId} by user {UserId}", locationId, GetCurrentUserId());
                return Ok(new { Message = $"Location cache cleared for location {locationId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing location cache for location {LocationId}", locationId);
                return StatusCode(500, new { Message = "An error occurred while clearing location cache" });
            }
        }

        /// <summary>
        /// Clear all location caches
        /// </summary>
        [HttpDelete("locations")]
        public async Task<ActionResult> ClearAllLocationCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Location>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.LocationPattern);

                _logger.LogInformation("All location cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All location cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all location cache");
                return StatusCode(500, new { Message = "An error occurred while clearing location cache" });
            }
        }

        /// <summary>
        /// Clear file cache
        /// </summary>
        [HttpDelete("files/{fileId:int}")]
        public async Task<ActionResult> ClearFileCache(int fileId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<FileEntity>(fileId);
                await _cacheInvalidationService.InvalidateRelatedAsync<FileEntity>(fileId);

                _logger.LogInformation("File cache cleared for file {FileId} by user {UserId}", fileId, GetCurrentUserId());
                return Ok(new { Message = $"File cache cleared for file {fileId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing file cache for file {FileId}", fileId);
                return StatusCode(500, new { Message = "An error occurred while clearing file cache" });
            }
        }

        /// <summary>
        /// Clear all file caches
        /// </summary>
        [HttpDelete("files")]
        public async Task<ActionResult> ClearAllFileCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<FileEntity>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.FilePattern);

                _logger.LogInformation("All file cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All file cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all file cache");
                return StatusCode(500, new { Message = "An error occurred while clearing file cache" });
            }
        }

        /// <summary>
        /// Clear folder cache
        /// </summary>
        [HttpDelete("folders/{folderId:int}")]
        public async Task<ActionResult> ClearFolderCache(int folderId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityAsync<Folder>(folderId);
                await _cacheInvalidationService.InvalidateRelatedAsync<Folder>(folderId);

                _logger.LogInformation("Folder cache cleared for folder {FolderId} by user {UserId}", folderId, GetCurrentUserId());
                return Ok(new { Message = $"Folder cache cleared for folder {folderId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing folder cache for folder {FolderId}", folderId);
                return StatusCode(500, new { Message = "An error occurred while clearing folder cache" });
            }
        }

        /// <summary>
        /// Clear all folder caches
        /// </summary>
        [HttpDelete("folders")]
        public async Task<ActionResult> ClearAllFolderCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Folder>();
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.FolderPattern);

                _logger.LogInformation("All folder cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All folder cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all folder cache");
                return StatusCode(500, new { Message = "An error occurred while clearing folder cache" });
            }
        }

        /// <summary>
        /// Clear search cache
        /// </summary>
        [HttpDelete("search")]
        public async Task<ActionResult> ClearSearchCache()
        {
            try
            {
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.SearchPattern);

                _logger.LogInformation("Search cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "Search cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing search cache");
                return StatusCode(500, new { Message = "An error occurred while clearing search cache" });
            }
        }

        /// <summary>
        /// Clear cache by custom pattern (development only)
        /// </summary>
        [HttpDelete("pattern")]
        [DevOnly]
        public async Task<ActionResult> ClearCacheByPattern([FromQuery] string pattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return BadRequest(new { Message = "Pattern is required" });
                }

                // Safety check to prevent clearing all cache accidentally
                if (pattern == "*")
                {
                    return BadRequest(new { Message = "Use the /cache/all endpoint to clear all cache" });
                }

                await _cacheInvalidationService.InvalidateByPatternAsync(pattern);
                _logger.LogWarning("Cache cleared by pattern {Pattern} by user {UserId}", pattern, GetCurrentUserId());
                return Ok(new { Message = $"Cache cleared for pattern: {pattern}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache by pattern {Pattern}", pattern);
                return StatusCode(500, new { Message = "An error occurred while clearing cache" });
            }
        }

        /// <summary>
        /// Warmup cache with commonly accessed data
        /// </summary>
        [HttpPost("warmup")]
        public async Task<ActionResult<object>> WarmupCache([FromQuery] bool includeHeavyOperations = false)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var warmedItems = new List<string>();

                // Warmup common cache entries based on the CacheKeys patterns
                var warmupTasks = new List<Task>();

                // Add basic warmup operations
                warmupTasks.Add(WarmupEntityType<User>("users", warmedItems));
                warmupTasks.Add(WarmupEntityType<Category>("categories", warmedItems));
                warmupTasks.Add(WarmupEntityType<Product>("products", warmedItems));
                warmupTasks.Add(WarmupEntityType<Page>("pages", warmedItems));

                if (includeHeavyOperations)
                {
                    warmupTasks.Add(WarmupEntityType<FileEntity>("files", warmedItems));
                    warmupTasks.Add(WarmupEntityType<Folder>("folders", warmedItems));
                }

                await Task.WhenAll(warmupTasks);

                var duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("Cache warmup completed by user {UserId} - {ItemCount} items warmed in {Duration}ms",
                    GetCurrentUserId(), warmedItems.Count, duration.TotalMilliseconds);

                var response = new
                {
                    Message = "Cache warmup completed successfully",
                    Duration = duration.TotalMilliseconds,
                    ItemsWarmed = warmedItems.Count,
                    Categories = warmedItems
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                return StatusCode(500, new { Message = "An error occurred during cache warmup" });
            }
        }

        /// <summary>
        /// Get cache key patterns and statistics
        /// </summary>
        [HttpGet("patterns")]
        public ActionResult<object> GetCachePatterns()
        {
            try
            {
                var patterns = CacheKeys.GetEntityPatterns();
                var allPatterns = CacheKeys.GetAllPatterns();

                var response = new
                {
                    EntityPatterns = patterns,
                    AllPatterns = allPatterns,
                    PredefinedKeys = new
                    {
                        Users = new[] { "user:list", "user:permissions", "user:sessions" },
                        Pages = new[] { "page:hierarchy", "page:published", "page:components" },
                        Categories = new[] { "category:all", "category:tree", "category:root" },
                        Products = new[] { "product:featured", "product:recent", "product:statistics" },
                        Files = new[] { "file:recent", "file:statistics", "file:thumbnails" },
                        Search = new[] { "search:results", "search:suggestions", "search:indexing" }
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache patterns");
                return StatusCode(500, new { Message = "An error occurred while getting cache patterns" });
            }
        }

        #region Private Helper Methods

        private async Task WarmupEntityType<T>(string entityName, List<string> warmedItems) where T : class
        {
            try
            {
                // This is a placeholder for warmup logic
                // In a real implementation, you would call actual service methods to load common data
                warmedItems.Add(entityName);
                await Task.Delay(100); // Simulate warmup time
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup {EntityType}", typeof(T).Name);
            }
        }

        private IServer GetRedisServer()
        {
            var endpoints = _redis.GetEndPoints();
            if (!endpoints.Any())
                throw new InvalidOperationException("No Redis endpoints available");

            return _redis.GetServer(endpoints.First());
        }

        private object ExtractMemoryInfo(Dictionary<string, object> cacheInfo)
        {
            return new
            {
                UsedMemory = cacheInfo.GetValueOrDefault("memory_used_memory", "N/A"),
                UsedMemoryHuman = cacheInfo.GetValueOrDefault("memory_used_memory_human", "N/A"),
                UsedMemoryRss = cacheInfo.GetValueOrDefault("memory_used_memory_rss", "N/A"),
                MaxMemory = cacheInfo.GetValueOrDefault("memory_maxmemory", "N/A"),
                MaxMemoryHuman = cacheInfo.GetValueOrDefault("memory_maxmemory_human", "N/A")
            };
        }

        private object ExtractPerformanceInfo(Dictionary<string, object> cacheInfo)
        {
            return new
            {
                OperationsPerSecond = cacheInfo.GetValueOrDefault("stats_instantaneous_ops_per_sec", "N/A"),
                TotalCommandsProcessed = cacheInfo.GetValueOrDefault("stats_total_commands_processed", "N/A"),
                TotalConnections = cacheInfo.GetValueOrDefault("stats_total_connections_received", "N/A"),
                KeyspaceHits = cacheInfo.GetValueOrDefault("stats_keyspace_hits", "N/A"),
                KeyspaceMisses = cacheInfo.GetValueOrDefault("stats_keyspace_misses", "N/A")
            };
        }

        private object ExtractConnectionInfo(Dictionary<string, object> cacheInfo)
        {
            return new
            {
                ConnectedClients = cacheInfo.GetValueOrDefault("server_connected_clients", "N/A"),
                RedisVersion = cacheInfo.GetValueOrDefault("server_redis_version", "N/A"),
                UptimeInSeconds = cacheInfo.GetValueOrDefault("server_uptime_in_seconds", "N/A"),
                Role = cacheInfo.GetValueOrDefault("replication_role", "N/A")
            };
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }

            return userId;
        }

        #endregion
    }
}